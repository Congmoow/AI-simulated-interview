using AiInterview.Api.DTOs.Positions;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;

namespace AiInterview.Api.Services;

public class PositionService(ICatalogRepository catalogRepository) : IPositionService
{
    public async Task<IReadOnlyCollection<PositionSummaryDto>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        var positions = await catalogRepository.GetActivePositionsAsync(cancellationToken);
        var result = new List<PositionSummaryDto>(positions.Count);

        foreach (var position in positions)
        {
            var count = await catalogRepository.CountQuestionsByPositionAsync(position.Code, cancellationToken);
            result.Add(ApplicationMapper.ToPositionSummaryDto(position, count));
        }

        return result;
    }

    public async Task<PositionDetailDto> GetPositionDetailAsync(string code, CancellationToken cancellationToken = default)
    {
        var position = await catalogRepository.GetPositionByCodeAsync(code, cancellationToken)
            ?? throw new AppException(Constants.ErrorCodes.PositionNotFound, "岗位不存在", StatusCodes.Status404NotFound);

        var questionCount = await catalogRepository.CountQuestionsByPositionAsync(code, cancellationToken);
        var typeCounts = await catalogRepository.GetQuestionTypeCountsAsync(code, cancellationToken);
        return ApplicationMapper.ToPositionDetailDto(position, questionCount, typeCounts);
    }
}

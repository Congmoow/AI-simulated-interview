using AiInterview.Api.DTOs.Positions;

namespace AiInterview.Api.Services.Interfaces;

public interface IPositionService
{
    Task<IReadOnlyCollection<PositionSummaryDto>> GetPositionsAsync(CancellationToken cancellationToken = default);

    Task<PositionDetailDto> GetPositionDetailAsync(string code, CancellationToken cancellationToken = default);
}

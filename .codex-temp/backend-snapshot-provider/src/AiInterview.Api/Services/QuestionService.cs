using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Questions;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;

namespace AiInterview.Api.Services;

public class QuestionService(ICatalogRepository catalogRepository) : IQuestionService
{
    public async Task<PagedResult<QuestionSummaryDto>> GetQuestionsAsync(string? positionCode, string? type, string? difficulty, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var questions = await catalogRepository.GetQuestionsAsync(positionCode, type, difficulty, safePage, safePageSize, cancellationToken);
        var total = await catalogRepository.CountQuestionsAsync(positionCode, type, difficulty, cancellationToken);

        return new PagedResult<QuestionSummaryDto>
        {
            Items = questions.Select(ApplicationMapper.ToQuestionSummaryDto).ToArray(),
            Total = total,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public async Task<QuestionDetailDto> GetQuestionDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var question = await catalogRepository.GetQuestionByIdAsync(id, cancellationToken)
            ?? throw new AppException(Constants.ErrorCodes.QuestionNotFound, "题目不存在", StatusCodes.Status404NotFound);

        var position = await catalogRepository.GetPositionByCodeAsync(question.PositionCode, cancellationToken);
        return ApplicationMapper.ToQuestionDetailDto(question, position?.Name ?? question.PositionCode);
    }
}

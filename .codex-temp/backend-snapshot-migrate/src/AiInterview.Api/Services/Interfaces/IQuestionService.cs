using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Questions;

namespace AiInterview.Api.Services.Interfaces;

public interface IQuestionService
{
    Task<PagedResult<QuestionSummaryDto>> GetQuestionsAsync(string? positionCode, string? type, string? difficulty, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<QuestionDetailDto> GetQuestionDetailAsync(Guid id, CancellationToken cancellationToken = default);
}

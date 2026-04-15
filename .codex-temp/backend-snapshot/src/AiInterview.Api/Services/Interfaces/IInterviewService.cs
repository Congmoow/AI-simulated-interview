using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Interviews;

namespace AiInterview.Api.Services.Interfaces;

public interface IInterviewService
{
    Task<CreateInterviewResponse> CreateInterviewAsync(Guid userId, CreateInterviewRequest request, CancellationToken cancellationToken = default);

    Task<InterviewCurrentDetailDto> GetInterviewAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default);

    Task<SubmitAnswerResponse> SubmitAnswerAsync(Guid userId, Guid interviewId, SubmitAnswerRequest request, CancellationToken cancellationToken = default);

    Task<FinishInterviewResponse> FinishInterviewAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default);

    Task<PagedResult<InterviewHistoryItemDto>> GetHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<InterviewDetailDto> GetInterviewDetailAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default);
}

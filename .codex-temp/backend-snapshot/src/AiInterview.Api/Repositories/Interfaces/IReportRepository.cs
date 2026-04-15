using AiInterview.Api.Models.Entities;

namespace AiInterview.Api.Repositories.Interfaces;

public interface IReportRepository
{
    Task<InterviewReport?> GetReportByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default);

    Task<InterviewScore?> GetScoreByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default);

    Task<List<InterviewReport>> GetUserReportsAsync(Guid userId, string? positionCode, CancellationToken cancellationToken = default);

    Task<RecommendationRecord?> GetLatestTrainingPlanAsync(Guid userId, Guid? interviewId, CancellationToken cancellationToken = default);

    Task AddOrUpdateScoreAsync(InterviewScore score, CancellationToken cancellationToken = default);

    Task AddOrUpdateReportAsync(InterviewReport report, CancellationToken cancellationToken = default);

    Task AddRecommendationRecordsAsync(IEnumerable<RecommendationRecord> records, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

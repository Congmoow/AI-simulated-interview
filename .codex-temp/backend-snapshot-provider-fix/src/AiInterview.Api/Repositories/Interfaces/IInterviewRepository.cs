using AiInterview.Api.Models.Entities;

namespace AiInterview.Api.Repositories.Interfaces;

public interface IInterviewRepository
{
    Task AddInterviewAsync(Interview interview, CancellationToken cancellationToken = default);

    Task AddRoundAsync(InterviewRound round, CancellationToken cancellationToken = default);

    Task<Interview?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Interview?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Interview>> GetUserHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountUserHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

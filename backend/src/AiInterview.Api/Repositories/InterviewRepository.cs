using AiInterview.Api.Data;
using AiInterview.Api.Constants;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Api.Repositories;

public class InterviewRepository(ApplicationDbContext dbContext) : IInterviewRepository
{
    public Task AddInterviewAsync(Interview interview, CancellationToken cancellationToken = default)
    {
        return dbContext.Interviews.AddAsync(interview, cancellationToken).AsTask();
    }

    public Task AddRoundAsync(InterviewRound round, CancellationToken cancellationToken = default)
    {
        return dbContext.InterviewRounds.AddAsync(round, cancellationToken).AsTask();
    }

    public Task<Interview?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Interviews
            .Include(x => x.Position)
            .Include(x => x.Score)
            .Include(x => x.Report)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Interview?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Interviews
            .Include(x => x.Position)
            .Include(x => x.Score)
            .Include(x => x.Report)
            .Include(x => x.Rounds.OrderBy(r => r.RoundNumber))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<List<Guid>> GetInterviewIdsPendingReportGenerationAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Interviews
            .Where(x => x.Status == InterviewStatuses.GeneratingReport)
            .Where(x => !dbContext.InterviewReports.Any(report => report.InterviewId == x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Interview>> GetUserHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return BuildHistoryQuery(userId, positionCode, status, startDate, endDate)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Position)
            .Include(x => x.Score)
            .Include(x => x.Rounds)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUserHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
    {
        return BuildHistoryQuery(userId, positionCode, status, startDate, endDate).CountAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Interview> BuildHistoryQuery(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate)
    {
        var query = dbContext.Interviews.Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            query = query.Where(x => x.PositionCode == positionCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (startDate.HasValue)
        {
            var start = startDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt <= end);
        }

        return query;
    }
}

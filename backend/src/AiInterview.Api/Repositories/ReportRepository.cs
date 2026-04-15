using AiInterview.Api.Data;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Api.Repositories;

public class ReportRepository(ApplicationDbContext dbContext) : IReportRepository
{
    public Task<InterviewReport?> GetReportByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return dbContext.InterviewReports
            .Include(x => x.Position)
            .FirstOrDefaultAsync(x => x.InterviewId == interviewId, cancellationToken);
    }

    public Task<InterviewScore?> GetScoreByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return dbContext.InterviewScores.FirstOrDefaultAsync(x => x.InterviewId == interviewId, cancellationToken);
    }

    public async Task<Dictionary<Guid, InterviewScore>> GetScoresByInterviewIdsAsync(IEnumerable<Guid> interviewIds, CancellationToken cancellationToken = default)
    {
        var ids = interviewIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await dbContext.InterviewScores
            .Where(x => ids.Contains(x.InterviewId))
            .ToDictionaryAsync(x => x.InterviewId, cancellationToken);
    }

    public Task<List<InterviewReport>> GetUserReportsAsync(Guid userId, string? positionCode, CancellationToken cancellationToken = default)
    {
        var query = dbContext.InterviewReports
            .Include(x => x.Position)
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            query = query.Where(x => x.PositionCode == positionCode);
        }

        return query.OrderBy(x => x.GeneratedAt).ToListAsync(cancellationToken);
    }

    public Task<RecommendationRecord?> GetLatestTrainingPlanAsync(Guid userId, Guid? interviewId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.RecommendationRecords
            .Where(x => x.UserId == userId && x.Type == "training_plan");

        if (interviewId.HasValue)
        {
            query = query.Where(x => x.InterviewId == interviewId.Value);
        }

        return query.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddOrUpdateScoreAsync(InterviewScore score, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.InterviewScores.FirstOrDefaultAsync(x => x.InterviewId == score.InterviewId, cancellationToken);
        if (existing is null)
        {
            await dbContext.InterviewScores.AddAsync(score, cancellationToken);
            return;
        }

        var existingId = existing.Id;
        dbContext.Entry(existing).CurrentValues.SetValues(score);
        existing.Id = existingId;
    }

    public async Task AddOrUpdateReportAsync(InterviewReport report, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.InterviewReports.FirstOrDefaultAsync(x => x.InterviewId == report.InterviewId, cancellationToken);
        if (existing is null)
        {
            await dbContext.InterviewReports.AddAsync(report, cancellationToken);
            return;
        }

        var existingId = existing.Id;
        dbContext.Entry(existing).CurrentValues.SetValues(report);
        existing.Id = existingId;
    }

    public Task AddRecommendationRecordsAsync(IEnumerable<RecommendationRecord> records, CancellationToken cancellationToken = default)
    {
        return dbContext.RecommendationRecords.AddRangeAsync(records, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

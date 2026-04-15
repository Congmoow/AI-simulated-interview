using AiInterview.Api.Data;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Api.Repositories;

public class CatalogRepository(ApplicationDbContext dbContext) : ICatalogRepository
{
    public Task<List<Position>> GetActivePositionsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Positions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Position?> GetPositionByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return dbContext.Positions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code && x.IsActive, cancellationToken);
    }

    public Task<List<QuestionBank>> GetQuestionsAsync(string? positionCode, string? type, string? difficulty, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return BuildQuestionQuery(positionCode, type, difficulty)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountQuestionsAsync(string? positionCode, string? type, string? difficulty, CancellationToken cancellationToken = default)
    {
        return BuildQuestionQuery(positionCode, type, difficulty).CountAsync(cancellationToken);
    }

    public Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.QuestionBanks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
    }

    public async Task<QuestionBank?> GetRandomQuestionAsync(string positionCode, IEnumerable<string> questionTypes, IEnumerable<Guid> excludedQuestionIds, CancellationToken cancellationToken = default)
    {
        var types = questionTypes.ToArray();
        var excluded = excludedQuestionIds.ToArray();

        var candidates = await dbContext.QuestionBanks
            .Where(x => x.IsActive && x.PositionCode == positionCode && types.Contains(x.Type) && !excluded.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            candidates = await dbContext.QuestionBanks
                .Where(x => x.IsActive && x.PositionCode == positionCode && types.Contains(x.Type))
                .ToListAsync(cancellationToken);
        }

        return candidates.Count == 0 ? null : candidates[Random.Shared.Next(candidates.Count)];
    }

    public Task<List<LearningResource>> GetLearningResourcesAsync(string? positionCode, IEnumerable<string>? dimensions, int limit, CancellationToken cancellationToken = default)
    {
        var query = dbContext.LearningResources
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            query = query.Where(x => x.PositionCode == positionCode);
        }

        var dims = dimensions?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (dims is { Length: > 0 })
        {
            query = query.Where(x => x.TargetDimensions.Any(dim => dims.Contains(dim)));
        }

        return query
            .OrderByDescending(x => x.Rating)
            .ThenBy(x => x.Title)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountQuestionsByPositionAsync(string positionCode, CancellationToken cancellationToken = default)
    {
        return dbContext.QuestionBanks.CountAsync(x => x.PositionCode == positionCode && x.IsActive, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetQuestionTypeCountsAsync(string positionCode, CancellationToken cancellationToken = default)
    {
        return await dbContext.QuestionBanks
            .Where(x => x.PositionCode == positionCode && x.IsActive)
            .GroupBy(x => x.Type)
            .Select(x => new { Type = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);
    }

    private IQueryable<QuestionBank> BuildQuestionQuery(string? positionCode, string? type, string? difficulty)
    {
        var query = dbContext.QuestionBanks.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            query = query.Where(x => x.PositionCode == positionCode);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => x.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            query = query.Where(x => x.Difficulty == difficulty);
        }

        return query;
    }
}

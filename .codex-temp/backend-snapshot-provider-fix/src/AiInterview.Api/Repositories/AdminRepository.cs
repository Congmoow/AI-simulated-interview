using AiInterview.Api.Data;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Api.Repositories;

public class AdminRepository(ApplicationDbContext dbContext) : IAdminRepository
{
    public Task AddQuestionAsync(QuestionBank question, CancellationToken cancellationToken = default)
    {
        return dbContext.QuestionBanks.AddAsync(question, cancellationToken).AsTask();
    }

    public Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.QuestionBanks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task AddKnowledgeDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        return dbContext.KnowledgeDocuments.AddAsync(document, cancellationToken).AsTask();
    }

    public Task<List<KnowledgeDocument>> GetKnowledgeDocumentsAsync(string? positionCode, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return BuildDocumentQuery(positionCode, status)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountKnowledgeDocumentsAsync(string? positionCode, string? status, CancellationToken cancellationToken = default)
    {
        return BuildDocumentQuery(positionCode, status).CountAsync(cancellationToken);
    }

    public async Task DeleteChunksByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var chunks = await dbContext.KnowledgeChunks
            .Where(x => x.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        dbContext.KnowledgeChunks.RemoveRange(chunks);
    }

    public Task AddKnowledgeChunksAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        return dbContext.KnowledgeChunks.AddRangeAsync(chunks, cancellationToken);
    }

    public async Task UpdateDocumentStatusAsync(Guid documentId, string status, int? chunkCount, DateTimeOffset? processedAt, string? error, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.KnowledgeDocuments
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        document.Status = status;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        if (chunkCount.HasValue)
        {
            document.ChunkCount = chunkCount.Value;
        }

        if (processedAt.HasValue)
        {
            document.ProcessedAt = processedAt.Value;
        }

        if (error is not null)
        {
            document.ProcessingError = error;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<KnowledgeDocument> BuildDocumentQuery(string? positionCode, string? status)
    {
        var query = dbContext.KnowledgeDocuments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            query = query.Where(x => x.PositionCode == positionCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        return query;
    }

    public Task<int> MarkStaleDocumentsAsFailedAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        return dbContext.KnowledgeDocuments
            .Where(d => d.Status == "processing" && d.CreatedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, "failed")
                    .SetProperty(d => d.ProcessingError,
                        d => d.ProcessingError == null ? "处理超时：任务长时间未完成" : d.ProcessingError)
                    .SetProperty(d => d.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }
}

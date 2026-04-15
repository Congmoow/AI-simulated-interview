using AiInterview.Api.Data;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Api.Repositories;

public class KnowledgeRepository(ApplicationDbContext dbContext) : IKnowledgeRepository
{
    public async Task<List<KnowledgeChunk>> SearchByKeywordAsync(
        string query,
        Guid? documentId,
        string? positionCode,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var q = dbContext.KnowledgeChunks
            .Include(x => x.Document)
            .AsQueryable();

        if (documentId.HasValue)
        {
            q = q.Where(x => x.DocumentId == documentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            q = q.Where(x => x.Document!.PositionCode == positionCode);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(x => EF.Functions.ILike(x.Content, $"%{query}%"));
        }

        return await q.Take(topK).ToListAsync(cancellationToken);
    }
}

using AiInterview.Api.Models.Entities;

namespace AiInterview.Api.Repositories.Interfaces;

public interface IKnowledgeRepository
{
    Task<List<KnowledgeChunk>> SearchByKeywordAsync(
        string query,
        Guid? documentId,
        string? positionCode,
        int topK,
        CancellationToken cancellationToken = default);
}

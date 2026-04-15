using AiInterview.Api.Models.Entities;

namespace AiInterview.Api.Repositories.Interfaces;

public interface IAdminRepository
{
    Task AddQuestionAsync(QuestionBank question, CancellationToken cancellationToken = default);

    Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddKnowledgeDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);

    Task<List<KnowledgeDocument>> GetKnowledgeDocumentsAsync(string? positionCode, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountKnowledgeDocumentsAsync(string? positionCode, string? status, CancellationToken cancellationToken = default);

    Task DeleteChunksByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task AddKnowledgeChunksAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);

    Task UpdateDocumentStatusAsync(Guid documentId, string status, int? chunkCount, DateTimeOffset? processedAt, string? error, CancellationToken cancellationToken = default);

    Task<int> MarkStaleDocumentsAsFailedAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

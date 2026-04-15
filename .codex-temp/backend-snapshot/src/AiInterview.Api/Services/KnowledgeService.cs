using AiInterview.Api.DTOs.Knowledge;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;

namespace AiInterview.Api.Services;

public class KnowledgeService(IKnowledgeRepository knowledgeRepository) : IKnowledgeService
{
    public async Task<KnowledgeSearchResponse> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
    {
        var safeTopK = Math.Clamp(request.TopK, 1, 20);

        var chunks = await knowledgeRepository.SearchByKeywordAsync(
            request.Query,
            request.DocumentId,
            request.PositionCode,
            safeTopK,
            cancellationToken);

        var items = chunks.Select((chunk, index) => new KnowledgeChunkDto
        {
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            DocumentTitle = chunk.Document?.Title ?? string.Empty,
            Content = chunk.Content,
            ChunkIndex = chunk.ChunkIndex,
            Score = Math.Round(1.0 - index * 0.05, 2)
        }).ToArray();

        return new KnowledgeSearchResponse
        {
            Items = items,
            Total = items.Length
        };
    }
}

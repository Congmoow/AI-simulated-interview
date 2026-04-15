using AiInterview.Api.DTOs.Knowledge;

namespace AiInterview.Api.Services.Interfaces;

public interface IKnowledgeService
{
    Task<KnowledgeSearchResponse> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default);
}

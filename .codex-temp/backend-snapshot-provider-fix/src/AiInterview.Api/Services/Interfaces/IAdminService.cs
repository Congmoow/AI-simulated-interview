using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Knowledge;

namespace AiInterview.Api.Services.Interfaces;

public interface IAdminService
{
    Task<QuestionAdminDto> CreateQuestionAsync(CreateQuestionRequest request, CancellationToken cancellationToken = default);

    Task<QuestionAdminDto> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request, CancellationToken cancellationToken = default);

    Task<UploadKnowledgeDocumentResponse> UploadKnowledgeDocumentAsync(Guid userId, UploadKnowledgeDocumentDto request, IFormFile file, CancellationToken cancellationToken = default);

    Task<PagedResult<KnowledgeDocumentListItemDto>> GetKnowledgeDocumentsAsync(string? positionCode, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task ProcessDocumentCallbackAsync(Guid documentId, DocumentProcessCallbackRequest request, CancellationToken cancellationToken = default);
}

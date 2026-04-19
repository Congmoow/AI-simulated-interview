using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Knowledge;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Options;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiInterview.Api.Services;

public class AdminService(
    IAdminRepository adminRepository,
    ICatalogRepository catalogRepository,
    IAiIntegrationService aiIntegrationService,
    IOptions<StorageOptions> storageOptions,
    ILogger<AdminService> logger) : IAdminService
{
    private readonly StorageOptions _storageOptions = storageOptions.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<QuestionAdminDto> CreateQuestionAsync(CreateQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var position = await catalogRepository.GetPositionByCodeAsync(request.PositionCode, cancellationToken)
            ?? throw new AppException(ErrorCodes.PositionNotFound, "岗位不存在", StatusCodes.Status404NotFound);

        var entity = new QuestionBank
        {
            PositionCode = position.Code,
            Type = request.Type,
            Difficulty = request.Difficulty,
            Title = request.Title,
            Content = request.Content,
            Tags = request.Tags,
            IdealAnswer = request.IdealAnswer,
            ScoringRubric = ApplicationMapper.SerializeObject(request.ScoringRubric),
            RelatedKnowledgeIds = request.RelatedKnowledgeIds ?? []
        };

        await adminRepository.AddQuestionAsync(entity, cancellationToken);
        await adminRepository.SaveChangesAsync(cancellationToken);
        return ApplicationMapper.ToQuestionAdminDto(entity);
    }

    public async Task<QuestionAdminDto> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await adminRepository.GetQuestionByIdAsync(id, cancellationToken)
            ?? throw new AppException(ErrorCodes.QuestionNotFound, "题目不存在", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            entity.Title = request.Title;
        }

        if (!string.IsNullOrWhiteSpace(request.Difficulty))
        {
            entity.Difficulty = request.Difficulty;
        }

        if (!string.IsNullOrWhiteSpace(request.IdealAnswer))
        {
            entity.IdealAnswer = request.IdealAnswer;
        }

        if (request.ScoringRubric is not null)
        {
            entity.ScoringRubric = ApplicationMapper.SerializeObject(request.ScoringRubric);
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await adminRepository.SaveChangesAsync(cancellationToken);
        return ApplicationMapper.ToQuestionAdminDto(entity);
    }

    public async Task<UploadKnowledgeDocumentResponse> UploadKnowledgeDocumentAsync(Guid userId, UploadKnowledgeDocumentDto request, IFormFile file, CancellationToken cancellationToken = default)
    {
        var position = await catalogRepository.GetPositionByCodeAsync(request.PositionCode, cancellationToken)
            ?? throw new AppException(ErrorCodes.PositionNotFound, "岗位不存在", StatusCodes.Status404NotFound);

        if (file.Length == 0)
        {
            throw new AppException(ErrorCodes.QuestionValidationFailed, "上传文件不能为空");
        }

        var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        var supported = new[] { "pdf", "txt", "md", "docx" };
        if (!supported.Contains(extension))
        {
            throw new AppException(ErrorCodes.QuestionValidationFailed, "仅支持 PDF/TXT/MD/DOCX 文档");
        }

        try
        {
            await KnowledgeFileSignatureValidator.EnsureValidAsync(file, extension, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            throw new AppException(ErrorCodes.QuestionValidationFailed, ex.Message);
        }

        var storageRoot = Path.GetFullPath(_storageOptions.KnowledgeRoot);
        Directory.CreateDirectory(storageRoot);

        var savedFileName = $"{Guid.NewGuid():N}.{extension}";
        var fullPath = Path.Combine(storageRoot, savedFileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var estimatedChunks = Math.Max(1, (int)Math.Ceiling(file.Length / 2048d));
        var document = new KnowledgeDocument
        {
            PositionCode = position.Code,
            Title = request.Title,
            FileUrl = savedFileName,
            FileType = extension,
            FileSize = file.Length,
            Tags = request.Tags,
            Status = "processing",
            CreatedBy = userId,
            Metadata = ApplicationMapper.SerializeObject(new
            {
                originalFileName = file.FileName,
                contentType = file.ContentType
            })
        };

        await adminRepository.AddKnowledgeDocumentAsync(document, cancellationToken);
        await adminRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await aiIntegrationService.EnqueueDocumentAsync(
                new EnqueueDocumentAiRequest
                {
                    DocumentId = document.Id,
                    FileName = savedFileName,
                    FileType = extension,
                    Title = document.Title
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "知识文档 {DocId} 入队失败，标记为 failed", document.Id);
            await adminRepository.UpdateDocumentStatusAsync(
                document.Id, "failed", null, null, $"入队失败: {ex.Message}", cancellationToken);
            await adminRepository.SaveChangesAsync(cancellationToken);
            throw new AppException(
                ErrorCodes.ServiceUnavailable,
                "文档入队失败，请稍后重试",
                StatusCodes.Status503ServiceUnavailable);
        }

        return new UploadKnowledgeDocumentResponse
        {
            DocumentId = document.Id,
            Title = document.Title,
            Status = document.Status,
            ChunkCount = document.ChunkCount,
            EstimatedChunks = estimatedChunks
        };
    }

    public async Task ProcessDocumentCallbackAsync(Guid documentId, DocumentProcessCallbackRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Status == "ready")
        {
            await adminRepository.DeleteChunksByDocumentIdAsync(documentId, cancellationToken);

            var chunks = request.Chunks.Select(c => new KnowledgeChunk
            {
                DocumentId = documentId,
                ChunkIndex = c.ChunkIndex,
                Content = c.Content,
                ContentHash = ComputeSha256(c.Content),
                Metadata = JsonSerializer.Serialize(c.Metadata, JsonOptions)
            }).ToList();

            await adminRepository.AddKnowledgeChunksAsync(chunks, cancellationToken);
            await adminRepository.UpdateDocumentStatusAsync(
                documentId, "ready", chunks.Count, DateTimeOffset.UtcNow, null, cancellationToken);

            logger.LogInformation("知识文档 {DocId} callback 落库完成，共 {ChunkCount} 个 chunks", documentId, chunks.Count);
        }
        else
        {
            await adminRepository.UpdateDocumentStatusAsync(
                documentId, "failed", null, null, request.Error, cancellationToken);

            logger.LogWarning("知识文档 {DocId} callback 状态为 failed，错误：{Error}", documentId, request.Error);
        }

        await adminRepository.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeSha256(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<PagedResult<KnowledgeDocumentListItemDto>> GetKnowledgeDocumentsAsync(string? positionCode, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var items = await adminRepository.GetKnowledgeDocumentsAsync(positionCode, status, safePage, safePageSize, cancellationToken);
        var total = await adminRepository.CountKnowledgeDocumentsAsync(positionCode, status, cancellationToken);

        return new PagedResult<KnowledgeDocumentListItemDto>
        {
            Items = items.Select(ApplicationMapper.ToKnowledgeDocumentListItemDto).ToArray(),
            Total = total,
            Page = safePage,
            PageSize = safePageSize
        };
    }
}

using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Middleware;
using AiInterview.Api.Options;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services;
using AiInterview.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Tests.Services;

file sealed class RecordingAdminRepository : IAdminRepository
{
    public List<KnowledgeDocument> AddedDocuments { get; } = [];

    public Task AddQuestionAsync(QuestionBank question, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<QuestionBank?>(null);

    public Task AddKnowledgeDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        AddedDocuments.Add(document);
        return Task.CompletedTask;
    }

    public Task<List<KnowledgeDocument>> GetKnowledgeDocumentsAsync(string? positionCode, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<KnowledgeDocument>());

    public Task<int> CountKnowledgeDocumentsAsync(string? positionCode, string? status, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task DeleteChunksByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddKnowledgeChunksAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateDocumentStatusAsync(Guid documentId, string status, int? chunkCount, DateTimeOffset? processedAt, string? error, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> MarkStaleDocumentsAsFailedAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class StubCatalogRepository : ICatalogRepository
{
    public Task<List<Position>> GetActivePositionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Position>());

    public Task<Position?> GetPositionByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult<Position?>(new Position
        {
            Code = code,
            Name = "测试岗位",
            Description = "测试岗位描述"
        });

    public Task<List<QuestionBank>> GetQuestionsAsync(string? positionCode, string? type, string? difficulty, int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<QuestionBank>());

    public Task<int> CountQuestionsAsync(string? positionCode, string? type, string? difficulty, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<QuestionBank?>(null);

    public Task<QuestionBank?> GetRandomQuestionAsync(string positionCode, IEnumerable<string> questionTypes, IEnumerable<Guid> excludedQuestionIds, CancellationToken cancellationToken = default)
        => Task.FromResult<QuestionBank?>(null);

    public Task<List<QuestionBank>> GetQuestionsByPositionAsync(string positionCode, IEnumerable<string> questionTypes, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<QuestionBank>());

    public Task<List<LearningResource>> GetLearningResourcesAsync(string? positionCode, IEnumerable<string>? dimensions, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<LearningResource>());

    public Task<int> CountQuestionsByPositionAsync(string positionCode, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<Dictionary<string, int>> GetQuestionTypeCountsAsync(string positionCode, CancellationToken cancellationToken = default)
        => Task.FromResult(new Dictionary<string, int>());
}

file sealed class StubAiIntegrationService : IAiIntegrationService
{
    public Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ReportAiResponse> GenerateReportAsync(ReportAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TrainingPlanAiResponse> GenerateTrainingPlanAsync(TrainingPlanAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ResourceRecommendationAiResponse> RecommendResourcesAsync(ResourceRecommendationAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProcessDocumentAiResponse> ProcessDocumentAsync(ProcessDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<EnqueueDocumentAiResponse> EnqueueDocumentAsync(EnqueueDocumentAiRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EnqueueDocumentAiResponse
        {
            TaskId = "task-1",
            DocumentId = request.DocumentId
        });
    }
}

public class AdminServiceUploadSecurityTests
{
    [Fact]
    public async Task UploadKnowledgeDocumentAsync_WhenPdfContentDoesNotMatchExtension_ShouldReject()
    {
        using var tempDir = new TempDirectory();
        var repository = new RecordingAdminRepository();
        var service = CreateService(repository, tempDir.Path);
        var file = CreateFormFile("resume.pdf", "application/pdf", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A]);

        var act = () => service.UploadKnowledgeDocumentAsync(
            Guid.NewGuid(),
            new UploadKnowledgeDocumentDto
            {
                Title = "测试 PDF",
                PositionCode = "java-backend"
            },
            file);

        var exception = await act.Should().ThrowAsync<AppException>();

        exception.Which.Code.Should().Be(ErrorCodes.QuestionValidationFailed);
        repository.AddedDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadKnowledgeDocumentAsync_WhenTextFileContainsBinaryBytes_ShouldReject()
    {
        using var tempDir = new TempDirectory();
        var repository = new RecordingAdminRepository();
        var service = CreateService(repository, tempDir.Path);
        var file = CreateFormFile("notes.txt", "text/plain", [0x48, 0x65, 0x00, 0x6C, 0x6C, 0x6F]);

        var act = () => service.UploadKnowledgeDocumentAsync(
            Guid.NewGuid(),
            new UploadKnowledgeDocumentDto
            {
                Title = "测试 TXT",
                PositionCode = "java-backend"
            },
            file);

        var exception = await act.Should().ThrowAsync<AppException>();

        exception.Which.Code.Should().Be(ErrorCodes.QuestionValidationFailed);
        repository.AddedDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadKnowledgeDocumentAsync_WhenPdfSignatureIsValid_ShouldAccept()
    {
        using var tempDir = new TempDirectory();
        var repository = new RecordingAdminRepository();
        var service = CreateService(repository, tempDir.Path);
        var file = CreateFormFile("resume.pdf", "application/pdf", "%PDF-1.7 测试内容"u8.ToArray());

        var result = await service.UploadKnowledgeDocumentAsync(
            Guid.NewGuid(),
            new UploadKnowledgeDocumentDto
            {
                Title = "合法 PDF",
                PositionCode = "java-backend"
            },
            file);

        result.Title.Should().Be("合法 PDF");
        repository.AddedDocuments.Should().ContainSingle();
    }

    private static AdminService CreateService(IAdminRepository repository, string storageRoot)
    {
        return new AdminService(
            repository,
            new StubCatalogRepository(),
            new StubAiIntegrationService(),
            Microsoft.Extensions.Options.Options.Create(new StorageOptions
            {
                KnowledgeRoot = storageRoot
            }),
            NullLogger<AdminService>.Instance);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ai-interview-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

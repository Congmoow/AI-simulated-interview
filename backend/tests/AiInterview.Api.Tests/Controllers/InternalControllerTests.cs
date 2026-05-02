using AiInterview.Api.Controllers;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Knowledge;
using AiInterview.Api.Options;
using AiInterview.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Tests.Controllers;

public class InternalControllerTests
{
    [Fact]
    public async Task GetAiRuntimeSettings_ShouldRejectRequest_WhenApiKeyMissing()
    {
        var controller = CreateController(
            new AiServiceOptions { ApiKey = string.Empty });

        var result = await controller.GetAiRuntimeSettings(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetAiRuntimeSettings_ShouldRejectRequest_WhenAuthorizationHeaderMismatch()
    {
        var controller = CreateController(
            new AiServiceOptions { ApiKey = "correct-key" },
            authorizationHeader: "Bearer wrong-key");

        var result = await controller.GetAiRuntimeSettings(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetAiRuntimeSettings_ShouldAllowRequest_WhenAuthorizationHeaderMatchesConfiguredApiKey()
    {
        const string apiKey = "shared-internal-api-key-1234567890";
        var controller = CreateController(
            new AiServiceOptions { ApiKey = apiKey },
            authorizationHeader: $"Bearer {apiKey}");

        var result = await controller.GetAiRuntimeSettings(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DocumentCallback_ShouldRejectRequest_WhenApiKeyMissing()
    {
        var controller = CreateController(
            new AiServiceOptions { ApiKey = string.Empty });
        var request = new DocumentProcessCallbackRequest
        {
            Status = "ready",
            Chunks = []
        };

        var result = await controller.DocumentCallback(Guid.NewGuid(), request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    private static InternalController CreateController(
        AiServiceOptions options,
        string? authorizationHeader = null)
    {
        var controller = new InternalController(
            new StubAdminService(),
            new StubAiSettingsService(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<InternalController>.Instance);

        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            httpContext.Request.Headers.Authorization = authorizationHeader;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private sealed class StubAdminService : IAdminService
    {
        public Task<QuestionAdminDto> CreateQuestionAsync(CreateQuestionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<QuestionAdminDto> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UploadKnowledgeDocumentResponse> UploadKnowledgeDocumentAsync(Guid userId, UploadKnowledgeDocumentDto request, IFormFile file, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PagedResult<KnowledgeDocumentListItemDto>> GetKnowledgeDocumentsAsync(string? positionCode, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ProcessDocumentCallbackAsync(Guid id, DocumentProcessCallbackRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubAiSettingsService : IAiSettingsService
    {
        public Task<AiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AiSettingsDto> UpdateSettingsAsync(UpdateAiSettingsRequest request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AiTestResult> TestConnectionAsync(TestAiConnectionRequest? request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IAiProvider?> BuildProviderAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AiRuntimeSettingsDto?> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AiRuntimeSettingsDto?>(new AiRuntimeSettingsDto
            {
                Provider = "mock",
                BaseUrl = "http://localhost:8000",
                Model = "mock-model",
                ApiKey = "masked",
                Temperature = 0.7m,
                MaxTokens = 2048,
                SystemPrompt = "test"
            });
        }
    }
}

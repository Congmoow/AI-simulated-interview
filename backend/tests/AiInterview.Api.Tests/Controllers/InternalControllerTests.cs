using AiInterview.Api.Controllers;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Knowledge;
using AiInterview.Api.Options;
using AiInterview.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Tests.Controllers;

public class InternalControllerTests
{
    [Fact]
    public async Task GetAiRuntimeSettings_ShouldRejectRequest_WhenApiKeyMissingAndBypassDisabled()
    {
        var controller = CreateController(
            new AiServiceOptions
            {
                ApiKey = string.Empty,
                AllowInsecureDevAuthBypass = false
            },
            environmentName: Environments.Development);

        var result = await controller.GetAiRuntimeSettings(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetAiRuntimeSettings_ShouldAllowRequest_WhenDevelopmentBypassEnabled()
    {
        var controller = CreateController(
            new AiServiceOptions
            {
                ApiKey = string.Empty,
                AllowInsecureDevAuthBypass = true
            },
            environmentName: Environments.Development);

        var result = await controller.GetAiRuntimeSettings(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DocumentCallback_ShouldRejectRequest_WhenBypassEnabledOutsideDevelopment()
    {
        var controller = CreateController(
            new AiServiceOptions
            {
                ApiKey = string.Empty,
                AllowInsecureDevAuthBypass = true
            },
            environmentName: Environments.Production);
        var request = new DocumentProcessCallbackRequest
        {
            Status = "ready",
            Chunks = []
        };

        var result = await controller.DocumentCallback(Guid.NewGuid(), request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetAiRuntimeSettings_ShouldAllowRequest_WhenAuthorizationHeaderMatchesConfiguredApiKey()
    {
        const string apiKey = "shared-internal-api-key-1234567890";
        var controller = CreateController(
            new AiServiceOptions
            {
                ApiKey = apiKey,
                AllowInsecureDevAuthBypass = false
            },
            environmentName: Environments.Production,
            authorizationHeader: $"Bearer {apiKey}");

        var result = await controller.GetAiRuntimeSettings(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    private static InternalController CreateController(
        AiServiceOptions options,
        string environmentName,
        string? authorizationHeader = null)
    {
        var controller = new InternalController(
            new StubAdminService(),
            new StubAiSettingsService(),
            Microsoft.Extensions.Options.Options.Create(options),
            new StubHostEnvironment(environmentName),
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

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "AiInterview.Api.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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

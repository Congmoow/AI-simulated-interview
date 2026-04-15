using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.Infrastructure;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services;
using AiInterview.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Api.Tests.Services;

file sealed class InMemoryAiSettingsRepository : IAiSettingsRepository
{
    public AiProviderSetting? Current { get; private set; }

    public InMemoryAiSettingsRepository(AiProviderSetting? current = null)
    {
        Current = current;
    }

    public Task<AiProviderSetting?> GetSingleAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Current);
    }

    public Task UpsertSingleAsync(AiProviderSetting entity, CancellationToken cancellationToken = default)
    {
        Current = entity;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

file sealed class StubApiKeyProtector : IApiKeyProtector
{
    private readonly bool _throwOnUnprotect;

    public StubApiKeyProtector(bool throwOnUnprotect = false)
    {
        _throwOnUnprotect = throwOnUnprotect;
    }

    public string Protect(string plainKey) => $"protected::{plainKey}";

    public string Unprotect(string protectedKey)
    {
        if (_throwOnUnprotect)
        {
            throw new InvalidOperationException("boom");
        }

        return protectedKey.Replace("protected::", string.Empty, StringComparison.Ordinal);
    }

    public string Mask(string plainKey) => $"mask::{plainKey[^4..]}";
}

public class AiSettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_WhenRepositoryIsEmpty_ShouldReturnDefaultDto()
    {
        var repository = new InMemoryAiSettingsRepository();
        var service = new AiSettingsService(
            repository,
            new StubApiKeyProtector(),
            NullLogger<AiSettingsService>.Instance);

        var result = await service.GetSettingsAsync();

        result.Should().NotBeNull();
        result.Provider.Should().Be("openai_compatible");
        result.IsEnabled.Should().BeFalse();
        result.IsKeyConfigured.Should().BeFalse();
        result.Temperature.Should().Be(0.7m);
        result.MaxTokens.Should().Be(2048);
        result.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenApiKeyIsBlank_ShouldKeepExistingProtectedKey()
    {
        var existing = new AiProviderSetting
        {
            Provider = "openai_compatible",
            BaseUrl = "https://example.com/v1",
            Model = "gpt-test",
            ApiKeyProtected = "protected::old-key",
            ApiKeyMasked = "mask::d-key",
            IsEnabled = true,
            Temperature = 0.8m,
            MaxTokens = 1024,
            SystemPrompt = "old prompt",
            UpdatedBy = "admin"
        };
        var repository = new InMemoryAiSettingsRepository(existing);
        var service = new AiSettingsService(
            repository,
            new StubApiKeyProtector(),
            NullLogger<AiSettingsService>.Instance);

        var result = await service.UpdateSettingsAsync(new UpdateAiSettingsRequest
        {
            Provider = "openai_compatible",
            BaseUrl = "https://new.example.com/v1",
            Model = "gpt-new",
            ApiKey = "   ",
            IsEnabled = true,
            Temperature = 0.2m,
            MaxTokens = 2048,
            SystemPrompt = "new prompt"
        }, "tester");

        repository.Current.Should().NotBeNull();
        repository.Current!.ApiKeyProtected.Should().Be("protected::old-key");
        repository.Current.ApiKeyMasked.Should().Be("mask::d-key");
        result.IsKeyConfigured.Should().BeTrue();
        result.ApiKeyMasked.Should().Be("mask::d-key");
    }

    [Fact]
    public async Task TestConnectionAsync_WhenConfigExistsButApiKeyMissing_ShouldReturnClearError()
    {
        var repository = new InMemoryAiSettingsRepository(new AiProviderSetting
        {
            Provider = "openai_compatible",
            BaseUrl = "https://example.com/v1",
            Model = "gpt-test",
            ApiKeyProtected = null,
            ApiKeyMasked = null,
            IsEnabled = true,
            UpdatedBy = "admin"
        });
        var service = new AiSettingsService(
            repository,
            new StubApiKeyProtector(),
            NullLogger<AiSettingsService>.Instance);

        var result = await service.TestConnectionAsync(null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("未配置 API Key，无法测试真实 LLM 连接");
    }

    [Fact]
    public async Task BuildProviderAsync_WhenEnabledButApiKeyMissing_ShouldReturnNull()
    {
        var repository = new InMemoryAiSettingsRepository(new AiProviderSetting
        {
            Provider = "openai_compatible",
            BaseUrl = "https://example.com/v1",
            Model = "gpt-test",
            ApiKeyProtected = null,
            IsEnabled = true,
            UpdatedBy = "admin"
        });
        var service = new AiSettingsService(
            repository,
            new StubApiKeyProtector(),
            NullLogger<AiSettingsService>.Instance);

        var result = await service.BuildProviderAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildProviderAsync_WhenApiKeyCannotBeUnprotected_ShouldReturnNull()
    {
        var repository = new InMemoryAiSettingsRepository(new AiProviderSetting
        {
            Provider = "openai_compatible",
            BaseUrl = "https://example.com/v1",
            Model = "gpt-test",
            ApiKeyProtected = "protected::fake-key",
            IsEnabled = true,
            UpdatedBy = "admin"
        });
        var service = new AiSettingsService(
            repository,
            new StubApiKeyProtector(throwOnUnprotect: true),
            NullLogger<AiSettingsService>.Instance);

        var result = await service.BuildProviderAsync();

        result.Should().BeNull();
    }
}

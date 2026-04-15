using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.Infrastructure;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;
using System.Diagnostics;

namespace AiInterview.Api.Services;

public class AiSettingsService(
    IAiSettingsRepository repository,
    IApiKeyProtector keyProtector,
    ILogger<AiSettingsService> logger) : IAiSettingsService
{
    public async Task<AiSettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await repository.GetSingleAsync(cancellationToken);
        return setting is null ? null : ToDto(setting);
    }

    public async Task<AiSettingsDto> UpdateSettingsAsync(UpdateAiSettingsRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetSingleAsync(cancellationToken);

        string? protectedKey = existing?.ApiKeyProtected;
        string? maskedKey = existing?.ApiKeyMasked;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            protectedKey = keyProtector.Protect(request.ApiKey);
            maskedKey = keyProtector.Mask(request.ApiKey);
        }

        var entity = new AiProviderSetting
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Provider = request.Provider,
            BaseUrl = request.BaseUrl,
            Model = request.Model,
            ApiKeyProtected = protectedKey,
            ApiKeyMasked = maskedKey,
            IsEnabled = request.IsEnabled,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            SystemPrompt = request.SystemPrompt,
            UpdatedBy = updatedBy,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await repository.UpsertSingleAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<AiTestResult> TestConnectionAsync(TestAiConnectionRequest? request, CancellationToken cancellationToken = default)
    {
        string? baseUrl = request?.BaseUrl;
        string? model = request?.Model;
        string? apiKey = request?.ApiKey;

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
        {
            var existing = await repository.GetSingleAsync(cancellationToken);
            if (existing is null)
            {
                return new AiTestResult { Success = false, ErrorMessage = "未找到 AI 配置，请先保存配置" };
            }

            baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? existing.BaseUrl : baseUrl;
            model = string.IsNullOrWhiteSpace(model) ? existing.Model : model;

            if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(existing.ApiKeyProtected))
            {
                try
                {
                    apiKey = keyProtector.Unprotect(existing.ApiKeyProtected);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("解密 API Key 失败，provider={Provider}: {Message}", existing.Provider, ex.Message);
                    return new AiTestResult { Success = false, ErrorMessage = "API Key 解密失败，请重新配置" };
                }
            }
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new AiTestResult { Success = false, ErrorMessage = "Base URL 不能为空" };
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var sw = Stopwatch.StartNew();
        try
        {
            var provider = new OpenAiCompatibleProvider(baseUrl, apiKey ?? string.Empty, model ?? string.Empty, 0.1f, 5);
            var chatRequest = new AiChatRequest
            {
                SystemPrompt = "You are a test assistant.",
                UserPrompt = "Reply with the word OK only.",
                MaxTokens = 5
            };

            await provider.ChatCompleteAsync(chatRequest, cts.Token);
            sw.Stop();

            return new AiTestResult { Success = true, LatencyMs = (int)sw.ElapsedMilliseconds };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiTestResult { Success = false, ErrorMessage = "连接超时（10 秒），请检查 Base URL 和网络环境" };
        }
        catch (Exception ex)
        {
            sw.Stop();
            var safeMessage = SanitizeErrorMessage(ex.Message, apiKey);
            logger.LogWarning("AI 连接测试失败，provider=openai_compatible, baseUrl={BaseUrl}: {Message}", baseUrl, safeMessage);
            return new AiTestResult { Success = false, ErrorMessage = safeMessage };
        }
    }

    public async Task<IAiProvider?> BuildProviderAsync(CancellationToken cancellationToken = default)
    {
        var setting = await repository.GetSingleAsync(cancellationToken);
        if (setting is null || !setting.IsEnabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(setting.BaseUrl) || string.IsNullOrWhiteSpace(setting.Model))
        {
            return null;
        }

        string apiKey = string.Empty;
        if (!string.IsNullOrWhiteSpace(setting.ApiKeyProtected))
        {
            try
            {
                apiKey = keyProtector.Unprotect(setting.ApiKeyProtected);
            }
            catch (Exception ex)
            {
                logger.LogWarning("构建 LLM Provider 时解密 API Key 失败，provider={Provider}: {Message}", setting.Provider, ex.Message);
                return null;
            }
        }

        return new OpenAiCompatibleProvider(
            setting.BaseUrl,
            apiKey,
            setting.Model,
            (float)setting.Temperature,
            setting.MaxTokens);
    }

    private static AiSettingsDto ToDto(AiProviderSetting setting) => new()
    {
        Id = setting.Id,
        Provider = setting.Provider,
        BaseUrl = setting.BaseUrl,
        Model = setting.Model,
        IsKeyConfigured = !string.IsNullOrWhiteSpace(setting.ApiKeyProtected),
        ApiKeyMasked = setting.ApiKeyMasked,
        IsEnabled = setting.IsEnabled,
        Temperature = setting.Temperature,
        MaxTokens = setting.MaxTokens,
        SystemPrompt = setting.SystemPrompt,
        UpdatedBy = setting.UpdatedBy,
        UpdatedAt = setting.UpdatedAt
    };

    private static string SanitizeErrorMessage(string message, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return message.Replace(apiKey, "[REDACTED]");
    }
}

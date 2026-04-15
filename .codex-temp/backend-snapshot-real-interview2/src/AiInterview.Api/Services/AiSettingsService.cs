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
    private const string DefaultProvider = "openai_compatible";
    private const decimal DefaultTemperature = 0.7m;
    private const int DefaultMaxTokens = 2048;
    private const string DefaultUpdatedBy = "system";
    private const string DefaultSystemPrompt =
        """
        你是一个资深技术面试评估师。限定返回格式为单一 JSON 对象，绝对不要输出任何 JSON 以外的内容。
        JSON 必须包含以下字段：
        - overallScore: 整体得分 0-100 的数字
        - dimensions: 各维度评分对象，每个字段包含 score(分数) 和 detail(详细评价)
        - strengths: 优势列表（字符串数组）
        - weaknesses: 不足列表（字符串数组）
        - suggestions: 具体改进建议列表（字符串数组）
        - summary: 总结性评价（字符串）
        """;

    public async Task<AiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await repository.GetSingleAsync(cancellationToken);
        return setting is null ? CreateDefaultDto() : ToDto(setting);
    }

    public async Task<AiSettingsDto> UpdateSettingsAsync(
        UpdateAiSettingsRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetSingleAsync(cancellationToken);

        string? protectedKey = existing?.ApiKeyProtected;
        string? maskedKey = existing?.ApiKeyMasked;
        var trimmedApiKey = request.ApiKey?.Trim();

        if (!string.IsNullOrWhiteSpace(trimmedApiKey))
        {
            protectedKey = keyProtector.Protect(trimmedApiKey);
            maskedKey = keyProtector.Mask(trimmedApiKey);
        }

        var entity = new AiProviderSetting
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Provider = string.IsNullOrWhiteSpace(request.Provider) ? DefaultProvider : request.Provider.Trim(),
            BaseUrl = request.BaseUrl.Trim(),
            Model = request.Model.Trim(),
            ApiKeyProtected = protectedKey,
            ApiKeyMasked = maskedKey,
            IsEnabled = request.IsEnabled,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            SystemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
                ? DefaultSystemPrompt
                : request.SystemPrompt.Trim(),
            UpdatedBy = updatedBy,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await repository.UpsertSingleAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<AiTestResult> TestConnectionAsync(
        TestAiConnectionRequest? request,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetSingleAsync(cancellationToken);

        var baseUrl = string.IsNullOrWhiteSpace(request?.BaseUrl)
            ? existing?.BaseUrl?.Trim()
            : request!.BaseUrl!.Trim();
        var model = string.IsNullOrWhiteSpace(request?.Model)
            ? existing?.Model?.Trim()
            : request!.Model!.Trim();
        var apiKey = request?.ApiKey?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(existing?.ApiKeyProtected))
        {
            try
            {
                apiKey = keyProtector.Unprotect(existing.ApiKeyProtected).Trim();
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "解密 API Key 失败，provider={Provider}，异常类型={ExceptionType}",
                    existing.Provider,
                    ex.GetType().Name);
                return new AiTestResult
                {
                    Success = false,
                    ErrorMessage = "API Key 解密失败，请重新保存配置后再测试连接"
                };
            }
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new AiTestResult { Success = false, ErrorMessage = "Base URL 不能为空" };
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return new AiTestResult { Success = false, ErrorMessage = "Model 不能为空" };
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiTestResult { Success = false, ErrorMessage = "未配置 API Key，无法测试真实 LLM 连接" };
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var sw = Stopwatch.StartNew();
        try
        {
            var provider = new OpenAiCompatibleProvider(baseUrl, apiKey, model, 0.1f, 5);
            await provider.ChatCompleteAsync(new AiChatRequest
            {
                SystemPrompt = "You are a test assistant.",
                UserPrompt = "Reply with the word OK only.",
                MaxTokens = 5
            }, cts.Token);
            sw.Stop();

            return new AiTestResult
            {
                Success = true,
                LatencyMs = (int)sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiTestResult
            {
                Success = false,
                ErrorMessage = "连接超时（10 秒），请检查 Base URL、模型名称和网络环境"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(
                "AI 连接测试失败，provider={Provider}，baseUrl={BaseUrl}，异常类型={ExceptionType}",
                existing?.Provider ?? request?.Provider ?? DefaultProvider,
                baseUrl,
                ex.GetType().Name);

            return new AiTestResult
            {
                Success = false,
                ErrorMessage = SanitizeErrorMessage(ex.Message, apiKey)
            };
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

        if (string.IsNullOrWhiteSpace(setting.ApiKeyProtected))
        {
            return null;
        }

        string apiKey;
        try
        {
            apiKey = keyProtector.Unprotect(setting.ApiKeyProtected).Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "构建 LLM Provider 时解密 API Key 失败，provider={Provider}，异常类型={ExceptionType}",
                setting.Provider,
                ex.GetType().Name);
            return null;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return new OpenAiCompatibleProvider(
            setting.BaseUrl.Trim(),
            apiKey,
            setting.Model.Trim(),
            (float)setting.Temperature,
            setting.MaxTokens);
    }

    public async Task<AiRuntimeSettingsDto?> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await repository.GetSingleAsync(cancellationToken);
        if (setting is null || !setting.IsEnabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(setting.BaseUrl)
            || string.IsNullOrWhiteSpace(setting.Model)
            || string.IsNullOrWhiteSpace(setting.ApiKeyProtected))
        {
            return null;
        }

        try
        {
            var apiKey = keyProtector.Unprotect(setting.ApiKeyProtected).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            return new AiRuntimeSettingsDto
            {
                Provider = setting.Provider,
                BaseUrl = setting.BaseUrl.Trim(),
                Model = setting.Model.Trim(),
                ApiKey = apiKey,
                Temperature = setting.Temperature,
                MaxTokens = setting.MaxTokens,
                SystemPrompt = string.IsNullOrWhiteSpace(setting.SystemPrompt) ? DefaultSystemPrompt : setting.SystemPrompt
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "读取运行时 AI 配置时解密 API Key 失败，provider={Provider}，异常类型={ExceptionType}",
                setting.Provider,
                ex.GetType().Name);
            return null;
        }
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
        SystemPrompt = string.IsNullOrWhiteSpace(setting.SystemPrompt) ? DefaultSystemPrompt : setting.SystemPrompt,
        UpdatedBy = setting.UpdatedBy,
        UpdatedAt = setting.UpdatedAt
    };

    private static AiSettingsDto CreateDefaultDto() => new()
    {
        Id = Guid.Empty,
        Provider = DefaultProvider,
        BaseUrl = string.Empty,
        Model = string.Empty,
        IsKeyConfigured = false,
        ApiKeyMasked = null,
        IsEnabled = false,
        Temperature = DefaultTemperature,
        MaxTokens = DefaultMaxTokens,
        SystemPrompt = DefaultSystemPrompt,
        UpdatedBy = DefaultUpdatedBy,
        UpdatedAt = DateTimeOffset.MinValue
    };

    private static string SanitizeErrorMessage(string message, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "连接真实 LLM 失败，请检查配置后重试";
        }

        return string.IsNullOrWhiteSpace(apiKey)
            ? message
            : message.Replace(apiKey, "[REDACTED]", StringComparison.Ordinal);
    }
}

using AiInterview.Api.Constants;
using AiInterview.Api.Middleware;
using AiInterview.Api.Options;
using AiInterview.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiInterview.Api.Services;

public class AiIntegrationService(HttpClient httpClient, IOptions<AiServiceOptions> options, ILogger<AiIntegrationService> logger) : IAiIntegrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiServiceOptions _options = options.Value;
    private readonly string _apiKey = options.Value.ApiKey?.Trim() ?? string.Empty;

    public async Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<StartInterviewAiRequest, StartInterviewAiResponse>("/interview/start", request, cancellationToken);
    }

    public async Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<AnswerAiRequest, AnswerAiResponse>("/interview/answer", request, cancellationToken);
    }

    public async Task<AnswerAiResponse?> AnswerStreamAsync(AnswerAiRequest request, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/interview/answer-stream")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("流式调用失败，status={Status}", (int)response.StatusCode);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            AnswerAiResponse? finalResult = null;
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;

                if (line.StartsWith("event: "))
                {
                    var eventType = line[7..].Trim();
                    var dataLine = await reader.ReadLineAsync(cancellationToken);
                    if (dataLine is null || !dataLine.StartsWith("data: ")) continue;
                    var data = dataLine[6..];

                    if (eventType == "chunk")
                    {
                        try
                        {
                            var chunkObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(data);
                            if (chunkObj.TryGetProperty("text", out var textProp))
                            {
                                await onChunk(textProp.GetString() ?? "");
                            }
                        }
                        catch { /* ignore parse errors */ }
                    }
                    else if (eventType == "done")
                    {
                        try
                        {
                            finalResult = System.Text.Json.JsonSerializer.Deserialize<AnswerAiResponse>(data, JsonOptions);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "解析流式最终结果失败");
                        }
                    }
                    else if (eventType == "error")
                    {
                        logger.LogWarning("流式调用错误: {Data}", data);
                        return null;
                    }
                }
            }

            return finalResult;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "流式调用异常，回退到普通模式");
            return null;
        }
    }

    public async Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<ScoreAiRequest, ScoreAiResponse>("/evaluation/score", request, cancellationToken);
    }

    public async Task<ReportAiResponse> GenerateReportAsync(ReportAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<ReportAiRequest, ReportAiResponse>("/report/generate", request, cancellationToken);
    }

    public async Task<ScoreAndReportAiResponse?> ScoreAndReportAsync(ScoreAiRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostAsync<ScoreAiRequest, ScoreAndReportAiResponse>("/evaluation/score-and-report", request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "合并评分报告调用失败，将回退到串行模式");
            return null;
        }
    }

    public async Task<TrainingPlanAiResponse> GenerateTrainingPlanAsync(TrainingPlanAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<TrainingPlanAiRequest, TrainingPlanAiResponse>("/recommend/training-plan", request, cancellationToken);
    }

    public async Task<ProcessDocumentAiResponse> ProcessDocumentAsync(ProcessDocumentAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<ProcessDocumentAiRequest, ProcessDocumentAiResponse>("/document/process", request, cancellationToken);
    }

    public async Task<EnqueueDocumentAiResponse> EnqueueDocumentAsync(EnqueueDocumentAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<EnqueueDocumentAiRequest, EnqueueDocumentAiResponse>("/document/enqueue", request, cancellationToken);
    }

    public async Task<ResourceRecommendationAiResponse> RecommendResourcesAsync(ResourceRecommendationAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<ResourceRecommendationAiRequest, ResourceRecommendationAiResponse>("/recommend/resources", request, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "调用 ai-service 失败：path={Path} status_code={StatusCode} body_snippet={BodySnippet}",
                path,
                (int)response.StatusCode,
                SummarizeBody(body));
            throw new AppException(ErrorCodes.ServiceUnavailable, "AI 服务暂不可用", StatusCodes.Status503ServiceUnavailable);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        return result ?? throw new AppException(ErrorCodes.ServiceUnavailable, "AI 服务返回数据为空", StatusCodes.Status503ServiceUnavailable);
    }

    private static string SummarizeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var compact = string.Join(' ', body.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 320 ? compact : compact[..320];
    }
}

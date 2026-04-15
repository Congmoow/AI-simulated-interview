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

    public async Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<StartInterviewAiRequest, StartInterviewAiResponse>("/interview/start", request, cancellationToken);
    }

    public async Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<AnswerAiRequest, AnswerAiResponse>("/interview/answer", request, cancellationToken);
    }

    public async Task<FinishInterviewAiResponse> FinishInterviewAsync(FinishInterviewAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<FinishInterviewAiRequest, FinishInterviewAiResponse>("/interview/finish", request, cancellationToken);
    }

    public async Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<ScoreAiRequest, ScoreAiResponse>("/evaluation/score", request, cancellationToken);
    }

    public async Task<ReportAiResponse> GenerateReportAsync(ReportAiRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAsync<ReportAiRequest, ReportAiResponse>("/report/generate", request, cancellationToken);
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

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
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

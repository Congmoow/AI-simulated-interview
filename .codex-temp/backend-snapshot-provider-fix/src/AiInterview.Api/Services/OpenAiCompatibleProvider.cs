using AiInterview.Api.Services.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiInterview.Api.Services;

public class OpenAiCompatibleProvider : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly float _temperature;
    private readonly int _maxTokens;

    public OpenAiCompatibleProvider(string baseUrl, string apiKey, string model, float temperature, int maxTokens)
    {
        _model = model;
        _temperature = temperature;
        _maxTokens = maxTokens;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<string> ChatCompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = request.Model ?? _model,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user",   content = request.UserPrompt   }
            },
            temperature = request.Temperature ?? _temperature,
            max_tokens  = request.MaxTokens   ?? _maxTokens
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? throw new InvalidOperationException("LLM 响应中 content 为空");
    }
}

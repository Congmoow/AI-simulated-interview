namespace AiInterview.Api.Services.Interfaces;

public class AiChatRequest
{
    public string SystemPrompt { get; init; } = string.Empty;

    public string UserPrompt { get; init; } = string.Empty;

    public string? Model { get; init; }

    public float? Temperature { get; init; }

    public int? MaxTokens { get; init; }
}

public interface IAiProvider
{
    Task<string> ChatCompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}

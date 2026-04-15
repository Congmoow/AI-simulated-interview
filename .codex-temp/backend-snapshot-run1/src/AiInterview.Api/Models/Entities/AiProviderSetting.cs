namespace AiInterview.Api.Models.Entities;

public class AiProviderSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Provider { get; set; } = "openai_compatible";

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? ApiKeyProtected { get; set; }

    public string? ApiKeyMasked { get; set; }

    public bool IsEnabled { get; set; } = false;

    public decimal Temperature { get; set; } = 0.7m;

    public int MaxTokens { get; set; } = 2048;

    public string SystemPrompt { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

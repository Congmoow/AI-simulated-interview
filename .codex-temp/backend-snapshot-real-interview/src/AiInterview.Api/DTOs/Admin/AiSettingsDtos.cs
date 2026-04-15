namespace AiInterview.Api.DTOs.Admin;

public class AiSettingsDto
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public bool IsKeyConfigured { get; init; }
    public string? ApiKeyMasked { get; init; }
    public bool IsEnabled { get; init; }
    public decimal Temperature { get; init; }
    public int MaxTokens { get; init; }
    public string SystemPrompt { get; init; } = string.Empty;
    public string UpdatedBy { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}

public class UpdateAiSettingsRequest
{
    public string Provider { get; set; } = "openai_compatible";
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsEnabled { get; set; }
    public decimal Temperature { get; set; } = 0.7m;
    public int MaxTokens { get; set; } = 2048;
    public string SystemPrompt { get; set; } = string.Empty;
}

public class TestAiConnectionRequest
{
    public string? Provider { get; set; }
    public string? BaseUrl { get; set; }
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
}

public class AiTestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? LatencyMs { get; init; }
}

public class AiRuntimeSettingsDto
{
    public string Provider { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public decimal Temperature { get; init; }
    public int MaxTokens { get; init; }
    public string SystemPrompt { get; init; } = string.Empty;
}

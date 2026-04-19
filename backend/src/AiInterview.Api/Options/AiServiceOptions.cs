namespace AiInterview.Api.Options;

public class AiServiceOptions
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = "http://localhost:8000";

    public string ApiKey { get; set; } = string.Empty;

    public bool AllowInsecureDevAuthBypass { get; set; }

    public int TimeoutSeconds { get; set; } = 60;
}

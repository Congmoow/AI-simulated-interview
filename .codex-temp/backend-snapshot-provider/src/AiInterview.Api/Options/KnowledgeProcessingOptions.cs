namespace AiInterview.Api.Options;

public class KnowledgeProcessingOptions
{
    public const string SectionName = "KnowledgeProcessing";

    public int StaleThresholdMinutes { get; set; } = 30;

    public int ScanIntervalMinutes { get; set; } = 5;
}

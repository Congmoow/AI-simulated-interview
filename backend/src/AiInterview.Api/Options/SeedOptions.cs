namespace AiInterview.Api.Options;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public string UserPassword { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;
}

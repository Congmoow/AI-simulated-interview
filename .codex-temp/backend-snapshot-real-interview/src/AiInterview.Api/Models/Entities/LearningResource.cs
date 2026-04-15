namespace AiInterview.Api.Models.Entities;

public class LearningResource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PositionCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Provider { get; set; }

    public string? Url { get; set; }

    public string? CoverUrl { get; set; }

    public string[] TargetDimensions { get; set; } = [];

    public string? Difficulty { get; set; }

    public string? Duration { get; set; }

    public string? ReadingTime { get; set; }

    public decimal? Rating { get; set; }

    public string[] Tags { get; set; } = [];

    public string Metadata { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Position? Position { get; set; }
}

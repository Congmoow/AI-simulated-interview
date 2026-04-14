namespace AiInterview.Api.Models.Entities;

public class InterviewScore
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InterviewId { get; set; }

    public decimal OverallScore { get; set; }

    public string DimensionScores { get; set; } = "{}";

    public string DimensionDetails { get; set; } = "{}";

    public decimal? RankPercentile { get; set; }

    public string ScoreBreakdown { get; set; } = "{}";

    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? ModelVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Interview? Interview { get; set; }
}

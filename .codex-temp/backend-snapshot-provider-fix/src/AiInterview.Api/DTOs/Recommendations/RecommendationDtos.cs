namespace AiInterview.Api.DTOs.Recommendations;

public class ResourceRecommendationDto
{
    public Guid ResourceId { get; set; }
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
    public decimal MatchScore { get; set; }
}

public class TrainingPlanDto
{
    public Guid PlanId { get; set; }
    public int Weeks { get; set; }
    public string DailyCommitment { get; set; } = string.Empty;
    public string[] Goals { get; set; } = [];
    public object[] Schedule { get; set; } = [];
    public object[] Milestones { get; set; } = [];
    public DateTimeOffset GeneratedAt { get; set; }
}

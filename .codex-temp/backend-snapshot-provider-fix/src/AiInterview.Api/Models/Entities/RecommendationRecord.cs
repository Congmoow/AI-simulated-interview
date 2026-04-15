namespace AiInterview.Api.Models.Entities;

public class RecommendationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid? InterviewId { get; set; }

    public Guid? ReportId { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid[] RecommendedResources { get; set; } = [];

    public string? TrainingPlan { get; set; }

    public string[] TargetDimensions { get; set; } = [];

    public string MatchScores { get; set; } = "{}";

    public string? Reason { get; set; }

    public bool IsViewed { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }

    public Interview? Interview { get; set; }

    public InterviewReport? Report { get; set; }
}

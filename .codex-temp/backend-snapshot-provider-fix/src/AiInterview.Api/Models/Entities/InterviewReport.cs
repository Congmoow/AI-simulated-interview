namespace AiInterview.Api.Models.Entities;

public class InterviewReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InterviewId { get; set; }

    public Guid UserId { get; set; }

    public string PositionCode { get; set; } = string.Empty;

    public decimal OverallScore { get; set; }

    public string? ExecutiveSummary { get; set; }

    public string[] Strengths { get; set; } = [];

    public string[] Weaknesses { get; set; } = [];

    public string DetailedAnalysis { get; set; } = "{}";

    public string[] LearningSuggestions { get; set; } = [];

    public string TrainingPlan { get; set; } = "{}";

    public string[] NextInterviewFocus { get; set; } = [];

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? ModelVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Interview? Interview { get; set; }

    public User? User { get; set; }

    public Position? Position { get; set; }
}

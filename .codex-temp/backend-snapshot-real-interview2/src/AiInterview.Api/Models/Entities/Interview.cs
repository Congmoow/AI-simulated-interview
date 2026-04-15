namespace AiInterview.Api.Models.Entities;

public class Interview
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string PositionCode { get; set; } = string.Empty;

    public string InterviewMode { get; set; } = Constants.InterviewModes.Standard;

    public string Status { get; set; } = Constants.InterviewStatuses.InProgress;

    public int TotalRounds { get; set; } = 5;

    public int CurrentRound { get; set; }

    public string[] QuestionTypes { get; set; } = Constants.QuestionTypes.All;

    public string Config { get; set; } = "{}";

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public int? DurationSeconds { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }

    public Position? Position { get; set; }

    public ICollection<InterviewRound> Rounds { get; set; } = [];

    public InterviewScore? Score { get; set; }

    public InterviewReport? Report { get; set; }
}

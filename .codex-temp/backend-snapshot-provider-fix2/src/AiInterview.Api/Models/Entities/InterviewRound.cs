namespace AiInterview.Api.Models.Entities;

public class InterviewRound
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InterviewId { get; set; }

    public int RoundNumber { get; set; }

    public Guid? QuestionId { get; set; }

    public string QuestionTitle { get; set; } = string.Empty;

    public string QuestionType { get; set; } = string.Empty;

    public string QuestionContent { get; set; } = string.Empty;

    public string? UserAnswer { get; set; }

    public string UserInputMode { get; set; } = "text";

    public string? VoiceTranscription { get; set; }

    public string[] AiFollowUps { get; set; } = [];

    public int FollowUpCount { get; set; }

    public string Context { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AnsweredAt { get; set; }

    public Interview? Interview { get; set; }

    public QuestionBank? Question { get; set; }
}

namespace AiInterview.Api.DTOs.Interviews;

public class CreateInterviewRequest
{
    public string PositionCode { get; set; } = string.Empty;
    public string InterviewMode { get; set; } = "standard";
    public string[]? QuestionTypes { get; set; }
    public int? RoundCount { get; set; }
}

public class CreateInterviewResponse
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string InterviewMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentRound { get; set; }
    public int TotalRounds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public QuestionBriefDto FirstQuestion { get; set; } = new();
    public IReadOnlyCollection<InterviewMessageDto> Messages { get; set; } = [];
}

public class QuestionBriefDto
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
}

public class InterviewCurrentDetailDto
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string InterviewMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentRound { get; set; }
    public int TotalRounds { get; set; }
    public IReadOnlyCollection<InterviewMessageDto> Messages { get; set; } = [];
    public IReadOnlyCollection<InterviewRoundCurrentDto> Rounds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public class InterviewMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid? RelatedQuestionId { get; set; }
    public int Sequence { get; set; }
    public object? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class InterviewRoundCurrentDto
{
    public int RoundNumber { get; set; }
    public RoundQuestionSummaryDto Question { get; set; } = new();
    public string? UserAnswer { get; set; }
    public string? AiFollowUp { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
}

public class RoundQuestionSummaryDto
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class SubmitAnswerRequest
{
    public string Answer { get; set; } = string.Empty;
    public string InputMode { get; set; } = "text";
    public string? Transcription { get; set; }
}

public class SubmitAnswerResponse
{
    public int RoundNumber { get; set; }
    public AiResponseDto AiResponse { get; set; } = new();
    public string InterviewStatus { get; set; } = string.Empty;
    public bool NextRoundAvailable { get; set; }
}

public class AiResponseDto
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[] Suggestions { get; set; } = [];
}

public class FinishInterviewResponse
{
    public Guid InterviewId { get; set; }
    public string Status { get; set; } = "generating_report";
    public Guid? ReportId { get; set; }
    public int EstimatedTime { get; set; }
}

public class InterviewHistoryItemDto
{
    public Guid InterviewId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public string InterviewMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? TotalScore { get; set; }
    public int RoundCount { get; set; }
    public int Duration { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class InterviewDetailDto
{
    public Guid InterviewId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public string InterviewMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? TotalScore { get; set; }
    public IReadOnlyCollection<InterviewRoundDetailDto> Rounds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class InterviewRoundDetailDto
{
    public int RoundNumber { get; set; }
    public InterviewQuestionDetailDto Question { get; set; } = new();
    public string? UserAnswer { get; set; }
    public string[] AiFollowUps { get; set; } = [];
    public object? Scores { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
}

public class InterviewQuestionDetailDto
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
}

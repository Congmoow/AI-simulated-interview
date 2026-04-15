namespace AiInterview.Api.DTOs.Questions;

public class QuestionSummaryDto
{
    public Guid Id { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public string? IdealAnswerHint { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class QuestionDetailDto
{
    public Guid Id { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public string? IdealAnswer { get; set; }
    public object ScoringRubric { get; set; } = new();
    public Guid[] RelatedKnowledgeIds { get; set; } = [];
}

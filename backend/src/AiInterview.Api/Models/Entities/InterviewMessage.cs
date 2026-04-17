namespace AiInterview.Api.Models.Entities;

public class InterviewMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InterviewId { get; set; }

    public string Role { get; set; } = Constants.InterviewMessageRoles.Assistant;

    public string MessageType { get; set; } = Constants.InterviewMessageTypes.Question;

    public string Content { get; set; } = string.Empty;

    public Guid? RelatedQuestionId { get; set; }

    public int Sequence { get; set; }

    public string Metadata { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Interview? Interview { get; set; }

    public QuestionBank? RelatedQuestion { get; set; }
}

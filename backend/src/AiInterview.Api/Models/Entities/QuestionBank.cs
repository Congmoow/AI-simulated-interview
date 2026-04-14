using NpgsqlTypes;

namespace AiInterview.Api.Models.Entities;

public class QuestionBank
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PositionCode { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string[] Tags { get; set; } = [];

    public string? IdealAnswer { get; set; }

    public string ScoringRubric { get; set; } = "{}";

    public Guid[] RelatedKnowledgeIds { get; set; } = [];

    public NpgsqlTsVector? SearchVector { get; set; }

    public int UseCount { get; set; }

    public int SuccessCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Position? Position { get; set; }
}

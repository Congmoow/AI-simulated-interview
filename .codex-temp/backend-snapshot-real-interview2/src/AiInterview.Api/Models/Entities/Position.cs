namespace AiInterview.Api.Models.Entities;

public class Position
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string[] Tags { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<QuestionBank> QuestionBanks { get; set; } = [];

    public ICollection<Interview> Interviews { get; set; } = [];

    public ICollection<KnowledgeDocument> KnowledgeDocuments { get; set; } = [];

    public ICollection<LearningResource> LearningResources { get; set; } = [];
}

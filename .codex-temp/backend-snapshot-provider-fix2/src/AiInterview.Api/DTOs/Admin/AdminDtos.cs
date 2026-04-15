namespace AiInterview.Api.DTOs.Admin;

public class CreateQuestionRequest
{
    public string PositionCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public string IdealAnswer { get; set; } = string.Empty;
    public Dictionary<string, decimal> ScoringRubric { get; set; } = [];
    public Guid[]? RelatedKnowledgeIds { get; set; }
}

public class UpdateQuestionRequest
{
    public string? Title { get; set; }
    public string? Difficulty { get; set; }
    public string? IdealAnswer { get; set; }
    public Dictionary<string, decimal>? ScoringRubric { get; set; }
}

public class QuestionAdminDto
{
    public Guid Id { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class UploadKnowledgeDocumentDto
{
    public string Title { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}

public class UploadKnowledgeDocumentResponse
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public int EstimatedChunks { get; set; }
}

public class KnowledgeDocumentListItemDto
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public string FileSize { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

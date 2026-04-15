namespace AiInterview.Api.Models.Entities;

public class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PositionCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string[] Tags { get; set; } = [];

    public string Status { get; set; } = "pending";

    public int ChunkCount { get; set; }

    public string? ProcessingError { get; set; }

    public string Metadata { get; set; } = "{}";

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Position? Position { get; set; }

    public User? Creator { get; set; }

    public ICollection<KnowledgeChunk> Chunks { get; set; } = [];
}

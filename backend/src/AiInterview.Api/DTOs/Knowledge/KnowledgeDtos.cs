using System.ComponentModel.DataAnnotations;

namespace AiInterview.Api.DTOs.Knowledge;

public class ChunkCallbackDto
{
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class DocumentProcessCallbackRequest
{
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    public List<ChunkCallbackDto> Chunks { get; set; } = [];

    [StringLength(2000)]
    public string? Error { get; set; }
}

public class KnowledgeSearchRequest
{
    [Required]
    [StringLength(2000)]
    public string Query { get; set; } = string.Empty;

    public Guid? DocumentId { get; set; }

    [StringLength(100)]
    public string? PositionCode { get; set; }

    [Range(1, 100)]
    public int TopK { get; set; } = 5;
}

public class KnowledgeChunkDto
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public double Score { get; set; }
}

public class KnowledgeSearchResponse
{
    public KnowledgeChunkDto[] Items { get; set; } = [];
    public int Total { get; set; }
}

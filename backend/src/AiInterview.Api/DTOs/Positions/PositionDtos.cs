namespace AiInterview.Api.DTOs.Positions;

public class PositionSummaryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int QuestionCount { get; set; }
    public string[] Tags { get; set; } = [];
}

public class PositionDetailDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public int QuestionCount { get; set; }
    public IReadOnlyCollection<QuestionTypeCountDto> QuestionTypes { get; set; } = [];
    public string[] Difficulty { get; set; } = [];
}

public class QuestionTypeCountDto
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

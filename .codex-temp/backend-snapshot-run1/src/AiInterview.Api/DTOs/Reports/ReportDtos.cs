namespace AiInterview.Api.DTOs.Reports;

public class InterviewReportDto
{
    public Guid ReportId { get; set; }
    public Guid InterviewId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public decimal OverallScore { get; set; }
    public Dictionary<string, DimensionScoreDto> DimensionScores { get; set; } = [];
    public string[] Strengths { get; set; } = [];
    public string[] Weaknesses { get; set; } = [];
    public string[] LearningSuggestions { get; set; } = [];
    public object[] TrainingPlan { get; set; } = [];
    public DateTimeOffset GeneratedAt { get; set; }
}

public class DimensionScoreDto
{
    public decimal Score { get; set; }
    public decimal Weight { get; set; }
}

public class GrowthDto
{
    public GrowthSummaryDto Summary { get; set; } = new();
    public IReadOnlyCollection<GrowthTrendItemDto> Trend { get; set; } = [];
    public IReadOnlyCollection<WeaknessTrackingDto> WeaknessTracking { get; set; } = [];
}

public class GrowthSummaryDto
{
    public int TotalInterviews { get; set; }
    public decimal AverageScore { get; set; }
    public decimal ScoreChange { get; set; }
    public string? StrongestDimension { get; set; }
    public string? WeakestDimension { get; set; }
}

public class GrowthTrendItemDto
{
    public DateOnly Date { get; set; }
    public decimal OverallScore { get; set; }
    public Dictionary<string, decimal> Dimensions { get; set; } = [];
}

public class WeaknessTrackingDto
{
    public string Dimension { get; set; } = string.Empty;
    public decimal CurrentScore { get; set; }
    public string Trend { get; set; } = string.Empty;
    public int RecentInterviews { get; set; }
}

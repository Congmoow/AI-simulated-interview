namespace AiInterview.Api.DTOs.Dashboard;

public class DashboardInsightsDto
{
    public DashboardOverviewDto Overview { get; set; } = new();
    public DashboardScopeDto Scope { get; set; } = new();
    public DashboardStrengthItemDto[] Strengths { get; set; } = [];
    public DashboardWeaknessItemDto[] Weaknesses { get; set; } = [];
    public DashboardAbilityDimension6Dto[] AbilityDimensions6 { get; set; } = [];
    public DashboardRecentTrendItemDto[] RecentTrend { get; set; } = [];
    public string[] NextActions { get; set; } = [];
}

public class DashboardOverviewDto
{
    public int TotalInterviews { get; set; }
    public int TotalReports { get; set; }
    public int Recent30DayInterviews { get; set; }
    public int StrengthsCount { get; set; }
    public int WeaknessesCount { get; set; }
    public string Trend { get; set; } = "flat";
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class DashboardScopeDto
{
    public string ScopeStrategy { get; set; } = string.Empty;
    public string ActualScope { get; set; } = string.Empty;
    public string? TargetPositionCode { get; set; }
    public string? TargetPositionName { get; set; }
    public bool FallbackTriggered { get; set; }
    public string? FallbackReason { get; set; }
    public int ReportCount { get; set; }
}

public class DashboardInsightSourceDto
{
    public Guid InterviewId { get; set; }
    public Guid ReportId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string PositionName { get; set; } = string.Empty;
}

public class DashboardStrengthItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EvidenceCount { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string[] EvidenceSamples { get; set; } = [];
    public DashboardInsightSourceDto[] Sources { get; set; } = [];
}

public class DashboardWeaknessItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EvidenceCount { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string[] TypicalBehaviors { get; set; } = [];
    public string Suggestion { get; set; } = string.Empty;
    public DashboardInsightSourceDto[] Sources { get; set; } = [];
}

public class DashboardAbilityDimension6Dto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string[] SourceDimensions { get; set; } = [];
}

public class DashboardRecentTrendItemDto
{
    public DateOnly Date { get; set; }
    public decimal Score { get; set; }
    public Guid InterviewId { get; set; }
    public Guid ReportId { get; set; }
}

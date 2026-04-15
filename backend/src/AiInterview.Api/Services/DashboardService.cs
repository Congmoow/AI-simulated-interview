using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Dashboard;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;

namespace AiInterview.Api.Services;

public class DashboardService(
    IUserRepository userRepository,
    IInterviewRepository interviewRepository,
    IReportRepository reportRepository) : IDashboardService
{
    private const int RecentTrendLimit = 10;
    private const int SourceLimit = 3;

    public async Task<DashboardInsightsDto> GetInsightsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppException(ErrorCodes.UserNotFound, "用户不存在", StatusCodes.Status404NotFound);

        var scope = await ResolveScopeAsync(userId, user, cancellationToken);
        var totalInterviews = await interviewRepository.CountUserHistoryAsync(
            userId,
            scope.Dto.ActualScope == DashboardInsightsRules.ActualScopeTargetPosition ? user.TargetPositionCode : null,
            null,
            null,
            null,
            cancellationToken);

        var recent30DayInterviews = await interviewRepository.CountUserHistoryAsync(
            userId,
            scope.Dto.ActualScope == DashboardInsightsRules.ActualScopeTargetPosition ? user.TargetPositionCode : null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            null,
            cancellationToken);

        if (scope.Reports.Count == 0)
        {
            return new DashboardInsightsDto
            {
                Overview = new DashboardOverviewDto
                {
                    TotalInterviews = totalInterviews,
                    TotalReports = 0,
                    Recent30DayInterviews = recent30DayInterviews,
                    StrengthsCount = 0,
                    WeaknessesCount = 0,
                    Trend = "flat",
                    UpdatedAt = null
                },
                Scope = scope.Dto,
                Strengths = [],
                Weaknesses = [],
                AbilityDimensions6 = [],
                RecentTrend = [],
                NextActions = []
            };
        }

        var scores = await reportRepository.GetScoresByInterviewIdsAsync(
            scope.Reports.Select(x => x.InterviewId),
            cancellationToken);

        var strengths = BuildStrengths(scope.Reports);
        var weaknesses = BuildWeaknesses(scope.Reports);
        var abilityDimensions6 = BuildAbilityDimensions(scope.Reports, scores);
        var recentTrend = BuildRecentTrend(scope.Reports, scores);
        var trend = BuildOverviewTrend(recentTrend);
        var nextActions = BuildNextActions(weaknesses, scope.Reports);

        return new DashboardInsightsDto
        {
            Overview = new DashboardOverviewDto
            {
                TotalInterviews = totalInterviews,
                TotalReports = scope.Reports.Count,
                Recent30DayInterviews = recent30DayInterviews,
                StrengthsCount = strengths.Length,
                WeaknessesCount = weaknesses.Length,
                Trend = trend,
                UpdatedAt = scope.Reports.Max(x => x.GeneratedAt)
            },
            Scope = scope.Dto,
            Strengths = strengths,
            Weaknesses = weaknesses,
            AbilityDimensions6 = abilityDimensions6,
            RecentTrend = recentTrend,
            NextActions = nextActions
        };
    }

    private async Task<(DashboardScopeDto Dto, List<InterviewReport> Reports)> ResolveScopeAsync(
        Guid userId,
        User user,
        CancellationToken cancellationToken)
    {
        var strategy = DashboardInsightsRules.ScopeStrategyTargetPreferredWithGlobalFallback;
        var targetPositionCode = user.TargetPositionCode;
        var targetPositionName = user.TargetPosition?.Name;

        if (!string.IsNullOrWhiteSpace(targetPositionCode))
        {
            var targetReports = await reportRepository.GetUserReportsAsync(userId, targetPositionCode, cancellationToken);
            if (targetReports.Count > 0)
            {
                return (new DashboardScopeDto
                {
                    ScopeStrategy = strategy,
                    ActualScope = DashboardInsightsRules.ActualScopeTargetPosition,
                    TargetPositionCode = targetPositionCode,
                    TargetPositionName = targetPositionName,
                    FallbackTriggered = false,
                    FallbackReason = null,
                    ReportCount = targetReports.Count
                }, targetReports);
            }

            var fallbackReports = await reportRepository.GetUserReportsAsync(userId, null, cancellationToken);
            return (new DashboardScopeDto
            {
                ScopeStrategy = strategy,
                ActualScope = DashboardInsightsRules.ActualScopeAllReports,
                TargetPositionCode = targetPositionCode,
                TargetPositionName = targetPositionName,
                FallbackTriggered = true,
                FallbackReason = DashboardInsightsRules.FallbackReasonTargetPositionHasNoReports,
                ReportCount = fallbackReports.Count
            }, fallbackReports);
        }

        var allReports = await reportRepository.GetUserReportsAsync(userId, null, cancellationToken);
        return (new DashboardScopeDto
        {
            ScopeStrategy = strategy,
            ActualScope = DashboardInsightsRules.ActualScopeAllReports,
            TargetPositionCode = null,
            TargetPositionName = null,
            FallbackTriggered = false,
            FallbackReason = null,
            ReportCount = allReports.Count
        }, allReports);
    }

    private static DashboardStrengthItemDto[] BuildStrengths(IReadOnlyCollection<InterviewReport> reports)
    {
        return DashboardInsightsRules.StrengthRules
            .Select(rule => BuildStrength(rule, reports))
            .Where(x => x is not null)
            .Cast<DashboardStrengthItemDto>()
            .OrderByDescending(x => x.EvidenceCount)
            .ThenByDescending(x => x.LastSeenAt)
            .ToArray();
    }

    private static DashboardStrengthItemDto? BuildStrength(
        DashboardStrengthRule rule,
        IReadOnlyCollection<InterviewReport> reports)
    {
        var matches = reports
            .Select(report => new
            {
                Report = report,
                Sample = MatchFirstText(GetStrengthTexts(report), rule.Keywords)
            })
            .Where(x => x.Sample is not null)
            .OrderByDescending(x => x.Report.GeneratedAt)
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        return new DashboardStrengthItemDto
        {
            Key = rule.Key,
            Title = rule.Title,
            Description = rule.Description,
            EvidenceCount = matches.Count,
            LastSeenAt = matches.Max(x => x.Report.GeneratedAt),
            EvidenceSamples = matches.Select(x => x.Sample!).Distinct().Take(SourceLimit).ToArray(),
            Sources = matches.Take(SourceLimit).Select(x => ToSourceDto(x.Report)).ToArray()
        };
    }

    private static DashboardWeaknessItemDto[] BuildWeaknesses(IReadOnlyCollection<InterviewReport> reports)
    {
        return DashboardInsightsRules.WeaknessRules
            .Select(rule => BuildWeakness(rule, reports))
            .Where(x => x is not null)
            .Cast<DashboardWeaknessItemDto>()
            .OrderByDescending(x => x.EvidenceCount)
            .ThenByDescending(x => x.LastSeenAt)
            .ToArray();
    }

    private static DashboardWeaknessItemDto? BuildWeakness(
        DashboardWeaknessRule rule,
        IReadOnlyCollection<InterviewReport> reports)
    {
        var matches = reports
            .Select(report => new
            {
                Report = report,
                Sample = MatchFirstText(GetWeaknessTexts(report), rule.Keywords)
            })
            .Where(x => x.Sample is not null)
            .OrderByDescending(x => x.Report.GeneratedAt)
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        var suggestion = rule.Suggestion;
        var suggestionMatch = matches
            .Select(x => MatchFirstText(GetWeaknessTexts(x.Report), [suggestion]))
            .FirstOrDefault(x => x is not null);
        if (!string.IsNullOrWhiteSpace(suggestionMatch))
        {
            suggestion = suggestionMatch;
        }

        return new DashboardWeaknessItemDto
        {
            Key = rule.Key,
            Title = rule.Title,
            Description = rule.Description,
            EvidenceCount = matches.Count,
            LastSeenAt = matches.Max(x => x.Report.GeneratedAt),
            TypicalBehaviors = rule.TypicalBehaviors,
            Suggestion = suggestion,
            Sources = matches.Take(SourceLimit).Select(x => ToSourceDto(x.Report)).ToArray()
        };
    }

    private static DashboardAbilityDimension6Dto[] BuildAbilityDimensions(
        IReadOnlyCollection<InterviewReport> reports,
        IReadOnlyDictionary<Guid, InterviewScore> scores)
    {
        var recentReports = reports.OrderByDescending(x => x.GeneratedAt).Take(5).ToList();
        return DashboardInsightsRules.AbilityDimensionRules
            .Select(rule =>
            {
                var values = recentReports
                    .Select(report => ReadMappedDimensionScore(scores.GetValueOrDefault(report.InterviewId), rule.SourceDimensions))
                    .Where(score => score.HasValue)
                    .Select(score => score!.Value)
                    .ToList();

                return values.Count == 0
                    ? null
                    : new DashboardAbilityDimension6Dto
                    {
                        Key = rule.Key,
                        Name = rule.Name,
                        Score = Math.Round(values.Average(), 2),
                        SourceDimensions = rule.SourceDimensions
                    };
            })
            .Where(x => x is not null)
            .Cast<DashboardAbilityDimension6Dto>()
            .ToArray();
    }

    private static DashboardRecentTrendItemDto[] BuildRecentTrend(
        IReadOnlyCollection<InterviewReport> reports,
        IReadOnlyDictionary<Guid, InterviewScore> scores)
    {
        return reports
            .OrderByDescending(x => x.GeneratedAt)
            .Take(RecentTrendLimit)
            .OrderBy(x => x.GeneratedAt)
            .Select(report => new
            {
                Report = report,
                Score = ResolveTrendScore(report, scores.GetValueOrDefault(report.InterviewId))
            })
            .Where(x => x.Score.HasValue)
            .Select(x => new DashboardRecentTrendItemDto
            {
                Date = DateOnly.FromDateTime(x.Report.GeneratedAt.UtcDateTime),
                Score = x.Score!.Value,
                InterviewId = x.Report.InterviewId,
                ReportId = x.Report.Id
            })
            .ToArray();
    }

    private static decimal? ResolveTrendScore(InterviewReport report, InterviewScore? score)
    {
        if (report.OverallScore > 0)
        {
            return Math.Round(report.OverallScore, 2);
        }

        var dimensions = ReadDimensionScores(score);
        var validScores = dimensions.Values
            .Select(x => x.Score)
            .Where(x => x > 0)
            .ToArray();

        return validScores.Length == 0 ? null : Math.Round(validScores.Average(), 2);
    }

    private static string BuildOverviewTrend(IReadOnlyList<DashboardRecentTrendItemDto> recentTrend)
    {
        if (recentTrend.Count < 2)
        {
            return "flat";
        }

        var latestGroup = recentTrend.TakeLast(Math.Min(3, recentTrend.Count)).Select(x => x.Score).ToArray();
        var previousGroup = recentTrend
            .Take(recentTrend.Count - latestGroup.Length)
            .TakeLast(Math.Min(3, recentTrend.Count - latestGroup.Length))
            .Select(x => x.Score)
            .ToArray();

        decimal delta;
        if (previousGroup.Length == 0)
        {
            delta = recentTrend[^1].Score - recentTrend[^2].Score;
        }
        else
        {
            delta = latestGroup.Average() - previousGroup.Average();
        }

        if (delta >= 3)
        {
            return "up";
        }

        if (delta <= -3)
        {
            return "down";
        }

        return "flat";
    }

    private static string[] BuildNextActions(
        IReadOnlyList<DashboardWeaknessItemDto> weaknesses,
        IReadOnlyCollection<InterviewReport> reports)
    {
        var actions = new List<string>();

        foreach (var weakness in weaknesses
                     .OrderByDescending(x => x.EvidenceCount)
                     .ThenByDescending(x => x.LastSeenAt)
                     .Select(x => x.Suggestion))
        {
            AppendUnique(actions, weakness);
            if (actions.Count == 3)
            {
                return actions.ToArray();
            }
        }

        foreach (var text in reports
                     .OrderByDescending(x => x.GeneratedAt)
                     .SelectMany(x => x.LearningSuggestions.Concat(x.NextInterviewFocus)))
        {
            AppendUnique(actions, text);
            if (actions.Count == 3)
            {
                return actions.ToArray();
            }
        }

        return actions.ToArray();
    }

    private static void AppendUnique(List<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (target.Contains(value))
        {
            return;
        }

        target.Add(value);
    }

    private static decimal? ReadMappedDimensionScore(InterviewScore? score, IReadOnlyCollection<string> sourceDimensions)
    {
        var dimensionScores = ReadDimensionScores(score);
        var values = sourceDimensions
            .Select(key => dimensionScores.GetValueOrDefault(key)?.Score)
            .Where(x => x.HasValue && x.Value > 0)
            .Select(x => x!.Value)
            .ToArray();

        return values.Length == 0 ? null : Math.Round(values.Average(), 2);
    }

    private static Dictionary<string, DimensionScoreDto> ReadDimensionScores(InterviewScore? score)
    {
        return ApplicationMapper.DeserializeObject<Dictionary<string, DimensionScoreDto>>(score?.DimensionScores, []);
    }

    private static IEnumerable<string> GetStrengthTexts(InterviewReport report)
    {
        return report.Strengths.Concat([report.ExecutiveSummary ?? string.Empty]);
    }

    private static IEnumerable<string> GetWeaknessTexts(InterviewReport report)
    {
        return report.Weaknesses
            .Concat(report.LearningSuggestions)
            .Concat(report.NextInterviewFocus)
            .Concat([report.ExecutiveSummary ?? string.Empty]);
    }

    private static string? MatchFirstText(IEnumerable<string> texts, IEnumerable<string> keywords)
    {
        var validTexts = texts.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        foreach (var text in validTexts)
        {
            if (keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static DashboardInsightSourceDto ToSourceDto(InterviewReport report)
    {
        return new DashboardInsightSourceDto
        {
            InterviewId = report.InterviewId,
            ReportId = report.Id,
            GeneratedAt = report.GeneratedAt,
            PositionName = report.Position?.Name ?? report.PositionCode
        };
    }
}

using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Dashboard;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiInterview.Api.Services;

public class DashboardService(
    IUserRepository userRepository,
    IInterviewRepository interviewRepository,
    IReportRepository reportRepository,
    IAiSettingsService aiSettingsService,
    IMemoryCache memoryCache,
    ILogger<DashboardService> logger) : IDashboardService
{
    private const int RecentTrendLimit = 10;
    private const int SourceLimit = 3;
    private const int ReportEvidenceLimit = 4;
    private const int InsightItemLimit = 3;
    private static readonly TimeSpan NarrativeCacheDuration = TimeSpan.FromMinutes(15);
    private const string DashboardInsightsSystemPrompt =
        """
        你是一名中文求职辅导顾问。
        请根据给定的面试画像信息，输出一个 JSON 对象，不要输出任何 JSON 以外的内容。
        JSON 结构必须为：
        {
          "heroSummary": "1 到 2 句、直接面向候选人的总结",
          "strengths": [
            {
              "title": "强项标题",
              "description": "强项描述",
              "evidenceSamples": ["证据摘要 1", "证据摘要 2"],
              "reportIndexes": [1, 2]
            }
          ],
          "weaknesses": [
            {
              "title": "短板标题",
              "description": "短板描述",
              "typicalBehaviors": ["典型表现 1", "典型表现 2"],
              "suggestion": "下一步建议",
              "reportIndexes": [1]
            }
          ],
          "nextActions": ["动作 1", "动作 2", "动作 3"]
        }
        要求：
        1. strengths 和 weaknesses 各返回 1 到 3 项。
        2. reportIndexes 只能引用输入里出现的报告编号。
        3. 输出必须具体、自然、可执行，不能空泛。
        4. 不要编造输入中不存在的证据。
        """;

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
                HeroSummary = BuildFallbackHeroSummary([], []),
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

        var fallbackStrengths = BuildStrengths(scope.Reports);
        var fallbackWeaknesses = BuildWeaknesses(scope.Reports);
        var abilityDimensions6 = BuildAbilityDimensions(scope.Reports, scores);
        var recentTrend = BuildRecentTrend(scope.Reports, scores);
        var trend = BuildOverviewTrend(recentTrend);
        var fallbackNextActions = BuildNextActions(fallbackWeaknesses, scope.Reports);

        var narrativeInsights = await BuildNarrativeInsightsAsync(
            userId,
            scope.Dto,
            scope.Reports,
            abilityDimensions6,
            recentTrend,
            fallbackStrengths,
            fallbackWeaknesses,
            fallbackNextActions,
            cancellationToken);

        return new DashboardInsightsDto
        {
            Overview = new DashboardOverviewDto
            {
                TotalInterviews = totalInterviews,
                TotalReports = scope.Reports.Count,
                Recent30DayInterviews = recent30DayInterviews,
                StrengthsCount = narrativeInsights.Strengths.Length,
                WeaknessesCount = narrativeInsights.Weaknesses.Length,
                Trend = trend,
                UpdatedAt = scope.Reports.Max(x => x.GeneratedAt)
            },
            Scope = scope.Dto,
            HeroSummary = narrativeInsights.HeroSummary,
            Strengths = narrativeInsights.Strengths,
            Weaknesses = narrativeInsights.Weaknesses,
            AbilityDimensions6 = abilityDimensions6,
            RecentTrend = recentTrend,
            NextActions = narrativeInsights.NextActions
        };
    }

    private async Task<DashboardNarrativeInsights> BuildNarrativeInsightsAsync(
        Guid userId,
        DashboardScopeDto scope,
        IReadOnlyCollection<InterviewReport> reports,
        IReadOnlyList<DashboardAbilityDimension6Dto> abilityDimensions,
        IReadOnlyList<DashboardRecentTrendItemDto> recentTrend,
        IReadOnlyList<DashboardStrengthItemDto> fallbackStrengths,
        IReadOnlyList<DashboardWeaknessItemDto> fallbackWeaknesses,
        IReadOnlyList<string> fallbackNextActions,
        CancellationToken cancellationToken)
    {
        var fallbackSummary = BuildFallbackHeroSummary(fallbackStrengths, fallbackWeaknesses);
        var fallbackInsights = new DashboardNarrativeInsights
        {
            HeroSummary = fallbackSummary,
            Strengths = fallbackStrengths.ToArray(),
            Weaknesses = fallbackWeaknesses.ToArray(),
            NextActions = fallbackNextActions.ToArray()
        };

        var aiSettings = await aiSettingsService.GetSettingsAsync(cancellationToken);
        if (!aiSettings.IsEnabled)
        {
            return fallbackInsights;
        }

        var cacheKey = BuildNarrativeInsightsCacheKey(
            userId,
            scope,
            reports,
            abilityDimensions,
            recentTrend,
            aiSettings);

        if (memoryCache.TryGetValue<DashboardNarrativeInsights>(cacheKey, out var cachedInsights) &&
            cachedInsights is not null)
        {
            return cachedInsights;
        }

        var provider = await aiSettingsService.BuildProviderAsync(cancellationToken);
        if (provider is null)
        {
            return fallbackInsights;
        }

        try
        {
            var rawContent = await provider.ChatCompleteAsync(new AiChatRequest
            {
                SystemPrompt = DashboardInsightsSystemPrompt,
                UserPrompt = BuildAiInsightsPrompt(scope, reports, abilityDimensions, recentTrend),
                Temperature = 0.2f,
                MaxTokens = 1200
            }, cancellationToken);

            var parsed = TryParseAiInsightsJson(rawContent);
            if (parsed is null)
            {
                var plainSummary = NormalizeHeroSummary(rawContent);
                var narrative = fallbackInsights with
                {
                    HeroSummary = string.IsNullOrWhiteSpace(plainSummary) ? fallbackSummary : plainSummary
                };
                memoryCache.Set(cacheKey, narrative, NarrativeCacheDuration);
                return narrative;
            }

            var orderedReports = reports
                .OrderByDescending(x => x.GeneratedAt)
                .Take(ReportEvidenceLimit)
                .OrderBy(x => x.GeneratedAt)
                .ToArray();

            var aiStrengths = MapAiStrengths(parsed.Strengths, orderedReports);
            var aiWeaknesses = MapAiWeaknesses(parsed.Weaknesses, orderedReports);
            var aiNextActions = BuildAiNextActions(parsed.NextActions, aiWeaknesses, fallbackNextActions);
            var aiSummary = NormalizeHeroSummary(parsed.HeroSummary);

            var result = new DashboardNarrativeInsights
            {
                HeroSummary = string.IsNullOrWhiteSpace(aiSummary) ? fallbackSummary : aiSummary,
                Strengths = aiStrengths.Length > 0 ? aiStrengths : fallbackStrengths.ToArray(),
                Weaknesses = aiWeaknesses.Length > 0 ? aiWeaknesses : fallbackWeaknesses.ToArray(),
                NextActions = aiNextActions.Length > 0 ? aiNextActions : fallbackNextActions.ToArray()
            };

            memoryCache.Set(cacheKey, result, NarrativeCacheDuration);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "生成个人画像 AI 聚合结果失败，回退到规则画像。reportCount={ReportCount}",
                reports.Count);
            return fallbackInsights;
        }
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

    private static string BuildAiInsightsPrompt(
        DashboardScopeDto scope,
        IReadOnlyCollection<InterviewReport> reports,
        IReadOnlyList<DashboardAbilityDimension6Dto> abilityDimensions,
        IReadOnlyList<DashboardRecentTrendItemDto> recentTrend)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"画像范围：{BuildScopeText(scope)}");
        builder.AppendLine($"纳入报告数：{scope.ReportCount}");
        builder.AppendLine($"六维画像：{BuildAbilityDimensionText(abilityDimensions)}");
        builder.AppendLine($"最近趋势：{BuildTrendText(recentTrend)}");
        builder.AppendLine("可引用的报告证据如下：");

        var orderedReports = reports
            .OrderByDescending(x => x.GeneratedAt)
            .Take(ReportEvidenceLimit)
            .OrderBy(x => x.GeneratedAt)
            .ToArray();

        for (var index = 0; index < orderedReports.Length; index += 1)
        {
            var report = orderedReports[index];
            builder.AppendLine(
                $"""
                报告#{index + 1}
                日期：{report.GeneratedAt:yyyy-MM-dd}
                岗位：{report.Position?.Name ?? report.PositionCode}
                总结：{TrimWithMarker(report.ExecutiveSummary, 90)}
                优势：{JoinReportTexts(report.Strengths)}
                不足：{JoinReportTexts(report.Weaknesses)}
                建议：{JoinReportTexts(report.LearningSuggestions.Concat(report.NextInterviewFocus).ToArray())}
                """);
        }

        builder.AppendLine("请严格按照 system prompt 中的 JSON 结构返回结果。");
        return builder.ToString();
    }

    private static DashboardStrengthItemDto[] MapAiStrengths(
        DashboardAiStrengthJson[]? items,
        IReadOnlyList<InterviewReport> orderedReports)
    {
        if (items is not { Length: > 0 })
        {
            return [];
        }

        var result = new List<DashboardStrengthItemDto>();
        for (var index = 0; index < items.Length && result.Count < InsightItemLimit; index += 1)
        {
            var item = items[index];
            var title = NormalizeSingleLine(item.Title, 20);
            var description = NormalizeSingleLine(item.Description, 120);
            var reportIndexes = NormalizeReportIndexes(item.ReportIndexes, orderedReports.Count);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) || reportIndexes.Length == 0)
            {
                continue;
            }

            var relatedReports = reportIndexes
                .Select(reportIndex => orderedReports[reportIndex - 1])
                .ToArray();

            result.Add(new DashboardStrengthItemDto
            {
                Key = $"ai_strength_{result.Count + 1}",
                Title = title,
                Description = description,
                EvidenceCount = reportIndexes.Length,
                LastSeenAt = relatedReports.Max(x => x.GeneratedAt),
                EvidenceSamples = NormalizeStringArray(item.EvidenceSamples, 3, 60),
                Sources = relatedReports
                    .OrderByDescending(x => x.GeneratedAt)
                    .Take(SourceLimit)
                    .Select(ToSourceDto)
                    .ToArray()
            });
        }

        return result.ToArray();
    }

    private static DashboardWeaknessItemDto[] MapAiWeaknesses(
        DashboardAiWeaknessJson[]? items,
        IReadOnlyList<InterviewReport> orderedReports)
    {
        if (items is not { Length: > 0 })
        {
            return [];
        }

        var result = new List<DashboardWeaknessItemDto>();
        for (var index = 0; index < items.Length && result.Count < InsightItemLimit; index += 1)
        {
            var item = items[index];
            var title = NormalizeSingleLine(item.Title, 20);
            var description = NormalizeSingleLine(item.Description, 120);
            var suggestion = NormalizeSingleLine(item.Suggestion, 80);
            var reportIndexes = NormalizeReportIndexes(item.ReportIndexes, orderedReports.Count);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) || reportIndexes.Length == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(suggestion))
            {
                suggestion = "结合最近一次面试复盘，补齐相关薄弱点。";
            }

            var relatedReports = reportIndexes
                .Select(reportIndex => orderedReports[reportIndex - 1])
                .ToArray();

            result.Add(new DashboardWeaknessItemDto
            {
                Key = $"ai_weakness_{result.Count + 1}",
                Title = title,
                Description = description,
                EvidenceCount = reportIndexes.Length,
                LastSeenAt = relatedReports.Max(x => x.GeneratedAt),
                TypicalBehaviors = NormalizeStringArray(item.TypicalBehaviors, 3, 60),
                Suggestion = suggestion,
                Sources = relatedReports
                    .OrderByDescending(x => x.GeneratedAt)
                    .Take(SourceLimit)
                    .Select(ToSourceDto)
                    .ToArray()
            });
        }

        return result.ToArray();
    }

    private static string[] BuildAiNextActions(
        string[]? aiNextActions,
        IReadOnlyList<DashboardWeaknessItemDto> aiWeaknesses,
        IReadOnlyList<string> fallbackNextActions)
    {
        var actions = new List<string>();

        foreach (var action in NormalizeStringArray(aiNextActions, 3, 80))
        {
            AppendUnique(actions, action);
        }

        foreach (var suggestion in aiWeaknesses.Select(x => x.Suggestion))
        {
            AppendUnique(actions, suggestion);
            if (actions.Count == 3)
            {
                return actions.ToArray();
            }
        }

        foreach (var fallback in fallbackNextActions)
        {
            AppendUnique(actions, fallback);
            if (actions.Count == 3)
            {
                return actions.ToArray();
            }
        }

        return actions.ToArray();
    }

    private static string BuildNarrativeInsightsCacheKey(
        Guid userId,
        DashboardScopeDto scope,
        IReadOnlyCollection<InterviewReport> reports,
        IReadOnlyList<DashboardAbilityDimension6Dto> abilityDimensions,
        IReadOnlyList<DashboardRecentTrendItemDto> recentTrend,
        AiSettingsDto aiSettings)
    {
        var builder = new StringBuilder();
        builder.Append(userId).Append('|');
        builder.Append(scope.ScopeStrategy).Append('|');
        builder.Append(scope.ActualScope).Append('|');
        builder.Append(scope.TargetPositionCode).Append('|');
        builder.Append(scope.TargetPositionName).Append('|');
        builder.Append(scope.FallbackTriggered).Append('|');
        builder.Append(scope.FallbackReason).Append('|');
        builder.Append(scope.ReportCount).Append('|');
        builder.Append(aiSettings.Provider).Append('|');
        builder.Append(aiSettings.BaseUrl).Append('|');
        builder.Append(aiSettings.Model).Append('|');
        builder.Append(aiSettings.UpdatedAt.ToUnixTimeMilliseconds());

        foreach (var report in reports.OrderBy(x => x.GeneratedAt).ThenBy(x => x.Id))
        {
            builder.Append('|');
            builder.Append(report.Id).Append(':');
            builder.Append(report.GeneratedAt.ToUnixTimeMilliseconds()).Append(':');
            builder.Append(report.UpdatedAt.ToUnixTimeMilliseconds()).Append(':');
            builder.Append(report.OverallScore.ToString(CultureInfo.InvariantCulture)).Append(':');
            builder.Append(report.PositionCode);
        }

        foreach (var item in abilityDimensions.OrderBy(x => x.Key))
        {
            builder.Append('|');
            builder.Append(item.Key).Append(':');
            builder.Append(item.Score.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var item in recentTrend.OrderBy(x => x.Date).ThenBy(x => x.ReportId))
        {
            builder.Append('|');
            builder.Append(item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(':');
            builder.Append(item.ReportId).Append(':');
            builder.Append(item.Score.ToString(CultureInfo.InvariantCulture));
        }

        return $"dashboard:insights:narrative:{HashCacheKey(builder.ToString())}";
    }

    private static string HashCacheKey(string rawValue)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(hash);
    }

    private static DashboardAiInsightsJson? TryParseAiInsightsJson(string rawContent)
    {
        static DashboardAiInsightsJson? TryDeserialize(string value)
        {
            try
            {
                return JsonSerializer.Deserialize<DashboardAiInsightsJson>(
                    value,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch
            {
                return null;
            }
        }

        var parsed = TryDeserialize(rawContent);
        if (parsed is not null)
        {
            return parsed;
        }

        var trimmed = rawContent.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                trimmed = trimmed[(firstNewLine + 1)..].TrimStart();
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3].TrimEnd();
            }
        }

        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            trimmed = trimmed[jsonStart..(jsonEnd + 1)];
        }

        return TryDeserialize(trimmed);
    }

    private static string BuildFallbackHeroSummary(
        IReadOnlyList<DashboardStrengthItemDto> strengths,
        IReadOnlyList<DashboardWeaknessItemDto> weaknesses)
    {
        var strengthSummary = JoinStrengthTitles(strengths);
        var weaknessSummary = JoinWeaknessTitles(weaknesses);

        if (!string.IsNullOrWhiteSpace(strengthSummary) && !string.IsNullOrWhiteSpace(weaknessSummary))
        {
            return $"根据你最近的面试记录，你当前更偏向“{strengthSummary}”，但 {weaknessSummary} 仍需继续加强。";
        }

        if (!string.IsNullOrWhiteSpace(strengthSummary))
        {
            return $"根据你最近的面试记录，你当前已经显现出“{strengthSummary}”等优势特征。";
        }

        if (!string.IsNullOrWhiteSpace(weaknessSummary))
        {
            return $"根据你最近的面试记录，当前最需要优先修复的是 {weaknessSummary}。";
        }

        return "根据你最近的面试记录，这里会持续更新你的能力强项、短板与趋势变化。";
    }

    private static string BuildScopeText(DashboardScopeDto scope)
    {
        if (scope.ActualScope == DashboardInsightsRules.ActualScopeTargetPosition)
        {
            return $"目标岗位 {scope.TargetPositionName ?? scope.TargetPositionCode ?? "未设置"}";
        }

        return scope.FallbackTriggered
            ? "目标岗位无报告，已回退到全部历史报告"
            : "全部历史报告";
    }

    private static string BuildAbilityDimensionText(IReadOnlyList<DashboardAbilityDimension6Dto> abilityDimensions)
    {
        if (abilityDimensions.Count == 0)
        {
            return "暂无";
        }

        return string.Join("；", abilityDimensions.Select(item => $"{item.Name} {item.Score:F0}"));
    }

    private static string BuildTrendText(IReadOnlyList<DashboardRecentTrendItemDto> recentTrend)
    {
        if (recentTrend.Count == 0)
        {
            return "暂无";
        }

        if (recentTrend.Count == 1)
        {
            var only = recentTrend[0];
            return $"{only.Date:MM-dd} {only.Score:F1}";
        }

        var latest = recentTrend[^1];
        var previous = recentTrend[Math.Max(0, recentTrend.Count - 2)];
        var direction = latest.Score > previous.Score ? "上升" : latest.Score < previous.Score ? "下降" : "持平";
        return $"{previous.Date:MM-dd} {previous.Score:F1} -> {latest.Date:MM-dd} {latest.Score:F1}（{direction}）";
    }

    private static string JoinStrengthTitles(IReadOnlyList<DashboardStrengthItemDto> strengths)
    {
        return string.Join("、", strengths.Take(2).Select(item => item.Title));
    }

    private static string JoinWeaknessTitles(IReadOnlyList<DashboardWeaknessItemDto> weaknesses)
    {
        return string.Join("、", weaknesses.Take(2).Select(item => item.Title));
    }

    private static string JoinReportTexts(string[] values)
    {
        var items = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(2)
            .Select(x => TrimWithMarker(x, 40))
            .ToArray();

        return items.Length == 0 ? "暂无" : string.Join("；", items);
    }

    private static string NormalizeHeroSummary(string? rawSummary)
    {
        return NormalizeSingleLine(rawSummary, 140);
    }

    private static string NormalizeSingleLine(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = string.Join(' ', value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .Trim('"', '\'', '“', '”');

        return TrimWithMarker(compact, limit, "…");
    }

    private static string[] NormalizeStringArray(IEnumerable<string>? values, int maxItems, int maxLength)
    {
        if (values is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var value in values)
        {
            var normalized = NormalizeSingleLine(value, maxLength);
            AppendUnique(result, normalized);
            if (result.Count == maxItems)
            {
                break;
            }
        }

        return result.ToArray();
    }

    private static int[] NormalizeReportIndexes(IEnumerable<int>? reportIndexes, int maxIndex)
    {
        if (reportIndexes is null)
        {
            return [];
        }

        return reportIndexes
            .Where(index => index >= 1 && index <= maxIndex)
            .Distinct()
            .ToArray();
    }

    private static string TrimWithMarker(string? value, int limit, string marker = "[TRUNCATED]")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= limit
            ? value
            : value[..Math.Max(0, limit - marker.Length)] + marker;
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

    private sealed record DashboardNarrativeInsights
    {
        public string HeroSummary { get; init; } = string.Empty;

        public DashboardStrengthItemDto[] Strengths { get; init; } = [];

        public DashboardWeaknessItemDto[] Weaknesses { get; init; } = [];

        public string[] NextActions { get; init; } = [];
    }

    private sealed class DashboardAiInsightsJson
    {
        public string? HeroSummary { get; init; }

        public DashboardAiStrengthJson[]? Strengths { get; init; }

        public DashboardAiWeaknessJson[]? Weaknesses { get; init; }

        public string[]? NextActions { get; init; }
    }

    private sealed class DashboardAiStrengthJson
    {
        public string? Title { get; init; }

        public string? Description { get; init; }

        public string[]? EvidenceSamples { get; init; }

        public int[]? ReportIndexes { get; init; }
    }

    private sealed class DashboardAiWeaknessJson
    {
        public string? Title { get; init; }

        public string? Description { get; init; }

        public string[]? TypicalBehaviors { get; init; }

        public string? Suggestion { get; init; }

        public int[]? ReportIndexes { get; init; }
    }
}

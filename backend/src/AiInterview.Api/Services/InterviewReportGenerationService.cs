using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Hubs;
using AiInterview.Api.Mappings;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AiInterview.Api.Services;

public sealed class InterviewReportGenerationService(
    IInterviewRepository interviewRepository,
    IReportRepository reportRepository,
    IAiIntegrationService aiIntegrationService,
    IAiSettingsService aiSettingsService,
    IHubContext<InterviewHub, IInterviewClient> hubContext,
    ILogger<InterviewReportGenerationService> logger) : IInterviewReportGenerationService
{
    private const string DefaultReportSystemPrompt =
        """
        你是一名中文技术面试复盘顾问。
        你必须只返回单个 JSON 对象，不能输出 JSON 以外的任何内容。
        JSON 必须包含以下字段：
        - overallScore: 0-100 的整体得分
        - dimensions: 各维度评分对象，每个维度至少包含 score 和 detail
        - strengths: 优势数组
        - weaknesses: 不足数组
        - suggestions: 改进建议数组
        - summary: 总结文本
        """;

    public async Task ProcessInterviewAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始处理报告生成任务，interviewId={InterviewId}", interviewId);

        try
        {
            var interview = await interviewRepository.GetByIdWithDetailsAsync(interviewId, cancellationToken);
            if (interview is null)
            {
                logger.LogWarning("报告生成任务对应的面试不存在，interviewId={InterviewId}", interviewId);
                return;
            }

            if (interview.Report is not null)
            {
                if (interview.Status != InterviewStatuses.Completed)
                {
                    interview.Status = InterviewStatuses.Completed;
                    interview.UpdatedAt = DateTimeOffset.UtcNow;
                    await interviewRepository.SaveChangesAsync(cancellationToken);
                }

                await PublishCompletedAsync(interview.Id, interview.Report.Id, cancellationToken);
                return;
            }

            var orderedRounds = BuildOrderedRounds(interview);
            var existingScoreEntity = interview.Score
                ?? await reportRepository.GetScoreByInterviewIdAsync(interview.Id, cancellationToken);
            var score = existingScoreEntity is not null
                ? MapExistingScore(existingScoreEntity)
                : await GenerateScoreAsync(interview, orderedRounds, cancellationToken);

            await PublishProgressAsync(interview.Id, 60, "reporting", 15, cancellationToken);
            var report = await GenerateReportWithFallbackAsync(interview, orderedRounds, score, cancellationToken);

            await PublishProgressAsync(interview.Id, 90, "saving", 5, cancellationToken);

            var scoreEntity = existingScoreEntity ?? new InterviewScore
            {
                InterviewId = interview.Id
            };
            scoreEntity.OverallScore = score.OverallScore;
            scoreEntity.DimensionScores = ApplicationMapper.SerializeObject(score.DimensionScores);
            scoreEntity.DimensionDetails = ApplicationMapper.SerializeObject(score.DimensionDetails);
            scoreEntity.ScoreBreakdown = ApplicationMapper.SerializeObject(score.ScoreBreakdown);
            scoreEntity.RankPercentile = score.RankPercentile;
            scoreEntity.ModelVersion = score.ModelVersion;
            scoreEntity.EvaluatedAt = DateTimeOffset.UtcNow;

            var reportEntity = interview.Report ?? new InterviewReport
            {
                InterviewId = interview.Id,
                UserId = interview.UserId,
                PositionCode = interview.PositionCode
            };
            reportEntity.OverallScore = score.OverallScore;
            reportEntity.ExecutiveSummary = report.ExecutiveSummary;
            reportEntity.Strengths = report.Strengths;
            reportEntity.Weaknesses = report.Weaknesses;
            reportEntity.DetailedAnalysis = ApplicationMapper.SerializeObject(report.DetailedAnalysis);
            reportEntity.LearningSuggestions = report.LearningSuggestions;
            reportEntity.TrainingPlan = ApplicationMapper.SerializeObject(report.TrainingPlan);
            reportEntity.NextInterviewFocus = report.NextInterviewFocus;
            reportEntity.ModelVersion = report.ModelVersion;
            reportEntity.GeneratedAt = DateTimeOffset.UtcNow;
            reportEntity.UpdatedAt = DateTimeOffset.UtcNow;

            await reportRepository.AddOrUpdateScoreAsync(scoreEntity, cancellationToken);
            await reportRepository.AddOrUpdateReportAsync(reportEntity, cancellationToken);

            interview.Status = InterviewStatuses.Completed;
            interview.UpdatedAt = DateTimeOffset.UtcNow;

            await reportRepository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "报告生成任务处理完成，interviewId={InterviewId} reportId={ReportId}",
                interview.Id,
                reportEntity.Id);

            await PublishCompletedAsync(interview.Id, reportEntity.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "报告生成任务失败，interviewId={InterviewId}", interviewId);
            await MarkInterviewFailedAsync(interviewId, cancellationToken);
        }
    }

    private async Task<ScoreAiResponse> GenerateScoreAsync(
        Interview interview,
        List<ScoreAiRoundDto> orderedRounds,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "开始评分，interviewId={InterviewId} roundCount={RoundCount}",
            interview.Id,
            orderedRounds.Count);
        await PublishProgressAsync(interview.Id, 30, "scoring", 20, cancellationToken);

        var sw = Stopwatch.StartNew();
        var score = await aiIntegrationService.ScoreAsync(new ScoreAiRequest
        {
            InterviewId = interview.Id,
            PositionCode = interview.PositionCode,
            Rounds = orderedRounds
        }, cancellationToken);
        sw.Stop();

        logger.LogInformation(
            "评分完成，interviewId={InterviewId} latencyMs={LatencyMs}",
            interview.Id,
            sw.ElapsedMilliseconds);

        return score;
    }

    private async Task<ReportAiResponse> GenerateReportWithFallbackAsync(
        Interview interview,
        List<ScoreAiRoundDto> orderedRounds,
        ScoreAiResponse score,
        CancellationToken cancellationToken)
    {
        var provider = await aiSettingsService.BuildProviderAsync(cancellationToken);
        if (provider is null)
        {
            logger.LogInformation("未启用直连模型，使用 ai-service 生成报告，interviewId={InterviewId}", interview.Id);
            return await FallbackToAiServiceReportAsync(interview, orderedRounds, score, cancellationToken);
        }

        var settings = await aiSettingsService.GetSettingsAsync(cancellationToken);
        var systemPrompt = !string.IsNullOrWhiteSpace(settings.SystemPrompt)
            ? settings.SystemPrompt
            : DefaultReportSystemPrompt;
        var userPrompt = BuildReportPrompt(interview, orderedRounds, score);
        var sw = Stopwatch.StartNew();

        try
        {
            var rawContent = await provider.ChatCompleteAsync(new AiChatRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                Temperature = 0.2f,
                MaxTokens = 1200
            }, cancellationToken);

            sw.Stop();
            logger.LogInformation(
                "直连模型生成报告完成，interviewId={InterviewId} latencyMs={LatencyMs}",
                interview.Id,
                sw.ElapsedMilliseconds);

            var parsed = TryParseReportJson(rawContent);
            if (parsed is null || !ValidateReportJson(parsed))
            {
                logger.LogWarning("直连模型返回结果不可用，改走 ai-service，interviewId={InterviewId}", interview.Id);
                return await FallbackToAiServiceReportAsync(interview, orderedRounds, score, cancellationToken);
            }

            return MapToReportAiResponse(parsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(
                ex,
                "直连模型生成报告失败，改走 ai-service，interviewId={InterviewId} latencyMs={LatencyMs}",
                interview.Id,
                sw.ElapsedMilliseconds);
            return await FallbackToAiServiceReportAsync(interview, orderedRounds, score, cancellationToken);
        }
    }

    private async Task<ReportAiResponse> FallbackToAiServiceReportAsync(
        Interview interview,
        List<ScoreAiRoundDto> orderedRounds,
        ScoreAiResponse score,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("report_fallback_used，interviewId={InterviewId}", interview.Id);

        return await aiIntegrationService.GenerateReportAsync(new ReportAiRequest
        {
            InterviewId = interview.Id,
            PositionCode = interview.PositionCode,
            OverallScore = score.OverallScore,
            DimensionScores = score.DimensionScores,
            DimensionDetails = score.DimensionDetails,
            Rounds = orderedRounds
        }, cancellationToken);
    }

    private static List<ScoreAiRoundDto> BuildOrderedRounds(Interview interview)
    {
        return interview.Rounds
            .OrderBy(x => x.RoundNumber)
            .Select(x => new ScoreAiRoundDto
            {
                RoundNumber = x.RoundNumber,
                QuestionType = x.QuestionType,
                QuestionTitle = x.QuestionTitle,
                QuestionContent = x.QuestionContent,
                Answer = x.UserAnswer,
                FollowUps = x.AiFollowUps
            })
            .ToList();
    }

    private static ScoreAiResponse MapExistingScore(InterviewScore score)
    {
        return new ScoreAiResponse
        {
            OverallScore = score.OverallScore,
            DimensionScores = ApplicationMapper.DeserializeObject<Dictionary<string, DimensionScoreDto>>(score.DimensionScores, []),
            DimensionDetails = ApplicationMapper.DeserializeObject<Dictionary<string, string>>(score.DimensionDetails, []),
            ScoreBreakdown = ApplicationMapper.DeserializeObject<Dictionary<string, object>>(score.ScoreBreakdown, []),
            RankPercentile = score.RankPercentile ?? 0,
            ModelVersion = score.ModelVersion ?? string.Empty
        };
    }

    private static string BuildReportPrompt(
        Interview interview,
        List<ScoreAiRoundDto> orderedRounds,
        ScoreAiResponse score)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"岗位：{interview.PositionCode}");
        sb.AppendLine($"总轮次：{interview.TotalRounds}");
        sb.AppendLine($"综合得分：{score.OverallScore:F1}");

        if (score.DimensionScores.Count > 0)
        {
            sb.AppendLine("维度得分：");
            foreach (var item in score.DimensionScores)
            {
                sb.AppendLine($"- {item.Key}: score={item.Value.Score:F1}, weight={item.Value.Weight:F2}");
            }
        }

        if (score.DimensionDetails.Count > 0)
        {
            sb.AppendLine("维度说明：");
            foreach (var item in score.DimensionDetails)
            {
                sb.AppendLine($"- {item.Key}: {TrimWithMarker(item.Value, 120)}");
            }
        }

        sb.AppendLine("问答摘要：");
        sb.AppendLine(BuildCompactRoundsSummary(orderedRounds, 30, 80, 120, 320, 1, 100, 1200));
        sb.AppendLine("请基于以上评分结果和问答摘要生成结构化复盘，只返回 JSON。");
        return sb.ToString();
    }

    private static string BuildCompactRoundsSummary(
        IEnumerable<ScoreAiRoundDto> rounds,
        int questionTypeLimit,
        int titleLimit,
        int contentLimit,
        int answerLimit,
        int followUpCount,
        int followUpLimit,
        int totalLimit)
    {
        var parts = new List<string>();
        foreach (var round in rounds)
        {
            var followUps = round.FollowUps?.TakeLast(followUpCount)
                .Select(item => TrimWithMarker(item, followUpLimit))
                .ToArray() ?? [];

            parts.Add(
                $"""
                Round {round.RoundNumber}
                Type: {TrimWithMarker(round.QuestionType, questionTypeLimit)}
                Title: {TrimWithMarker(round.QuestionTitle, titleLimit)}
                Content: {TrimWithMarker(round.QuestionContent, contentLimit)}
                Answer: {TrimWithMarker(round.Answer ?? "N/A", answerLimit)}
                FollowUps: {(followUps.Length == 0 ? "N/A" : string.Join(" | ", followUps))}
                """);
        }

        var summary = string.Join(Environment.NewLine + Environment.NewLine, parts);
        return TrimWithMarker(summary, totalLimit);
    }

    private static string TrimWithMarker(string value, int limit, string marker = "[TRUNCATED]")
    {
        if (string.IsNullOrEmpty(value) || value.Length <= limit)
        {
            return value;
        }

        return value[..Math.Max(0, limit - marker.Length)] + marker;
    }

    private async Task PublishProgressAsync(
        Guid interviewId,
        int progress,
        string stage,
        int estimatedTime,
        CancellationToken cancellationToken)
    {
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interviewId)).ReportProgress(new
        {
            progress,
            stage,
            estimatedTime
        });
    }

    private async Task PublishCompletedAsync(Guid interviewId, Guid reportId, CancellationToken cancellationToken)
    {
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interviewId)).InterviewStatusChanged(new
        {
            status = InterviewStatuses.Completed
        });
        await PublishProgressAsync(interviewId, 100, "completed", 0, cancellationToken);
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interviewId)).ReportReady(new
        {
            reportId
        });
    }

    private async Task MarkInterviewFailedAsync(Guid interviewId, CancellationToken cancellationToken)
    {
        var interview = await interviewRepository.GetByIdAsync(interviewId, cancellationToken);
        if (interview is not null)
        {
            interview.Status = InterviewStatuses.ReportFailed;
            interview.UpdatedAt = DateTimeOffset.UtcNow;
            await interviewRepository.SaveChangesAsync(cancellationToken);
        }

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interviewId)).InterviewStatusChanged(new
        {
            status = InterviewStatuses.ReportFailed
        });
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interviewId)).ErrorOccurred(new
        {
            stage = "report_failed",
            message = "报告生成失败，请稍后重试。"
        });
    }

    private static LlmReportJson? TryParseReportJson(string raw)
    {
        static LlmReportJson? TryDeserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<LlmReportJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch
            {
                return null;
            }
        }

        var result = TryDeserialize(raw);
        if (result is not null)
        {
            return result;
        }

        var trimmed = raw.Trim();
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

    private static bool ValidateReportJson(LlmReportJson json)
    {
        return json.Strengths is { Length: > 0 }
            && json.Weaknesses is { Length: > 0 }
            && (json.Suggestions is { Length: > 0 } || json.LearningSuggestions is { Length: > 0 })
            && (!string.IsNullOrWhiteSpace(json.Summary) || !string.IsNullOrWhiteSpace(json.ExecutiveSummary));
    }

    private static ReportAiResponse MapToReportAiResponse(LlmReportJson json)
    {
        return new ReportAiResponse
        {
            ExecutiveSummary = !string.IsNullOrWhiteSpace(json.ExecutiveSummary)
                ? json.ExecutiveSummary
                : json.Summary ?? string.Empty,
            Strengths = json.Strengths ?? [],
            Weaknesses = json.Weaknesses ?? [],
            LearningSuggestions = json.LearningSuggestions is { Length: > 0 }
                ? json.LearningSuggestions
                : json.Suggestions ?? [],
            DetailedAnalysis = json.DetailedAnalysis is not null
                ? json.DetailedAnalysis.ToDictionary(k => k.Key, v => (object)v.Value)
                : json.Dimensions is not null
                    ? json.Dimensions.ToDictionary(k => k.Key, v => (object)v.Value)
                    : [],
            TrainingPlan = json.TrainingPlan?.Cast<object>().ToArray() ?? [],
            NextInterviewFocus = json.NextInterviewFocus ?? [],
            ModelVersion = "llm-v1"
        };
    }

    private sealed class LlmReportJson
    {
        public decimal OverallScore { get; init; }

        public Dictionary<string, JsonElement>? Dimensions { get; init; }

        public string? ExecutiveSummary { get; init; }

        public string[]? Strengths { get; init; }

        public string[]? Weaknesses { get; init; }

        public string[]? Suggestions { get; init; }

        public Dictionary<string, JsonElement>? DetailedAnalysis { get; init; }

        public string[]? LearningSuggestions { get; init; }

        public JsonElement[]? TrainingPlan { get; init; }

        public string[]? NextInterviewFocus { get; init; }

        public string? Summary { get; init; }
    }
}

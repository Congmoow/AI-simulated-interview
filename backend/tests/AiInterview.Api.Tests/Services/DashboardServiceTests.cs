using System.Text.Json;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services;
using AiInterview.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Api.Tests.Services;

sealed class InMemoryDashboardUserRepository : IUserRepository
{
    public User? User { get; set; }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(User?.Id == id ? User : null);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(User?.Username == username ? User : null);
    }

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(User?.Username == username);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(User?.Email == email);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        User = user;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class InMemoryDashboardInterviewRepository : IInterviewRepository
{
    public List<Interview> Interviews { get; } = [];

    public Task AddInterviewAsync(Interview interview, CancellationToken cancellationToken = default)
    {
        Interviews.Add(interview);
        return Task.CompletedTask;
    }

    public Task AddRoundAsync(InterviewRound round, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task AddMessageAsync(InterviewMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<Interview?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Interviews.FirstOrDefault(x => x.Id == id));
    }

    public Task<Interview?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Interviews.FirstOrDefault(x => x.Id == id));
    }

    public Task<List<InterviewMessage>> GetMessagesAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<InterviewMessage>());
    }

    public Task<int> GetNextMessageSequenceAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(1);
    }

    public Task<List<Guid>> GetInterviewIdsPendingReportGenerationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<Guid>());
    }

    public Task<List<Interview>> GetUserHistoryAsync(
        Guid userId,
        string? positionCode,
        string? status,
        DateOnly? startDate,
        DateOnly? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilteredQuery(userId, positionCode, status, startDate, endDate);
        return Task.FromResult(query.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    public Task<int> CountUserHistoryAsync(
        Guid userId,
        string? positionCode,
        string? status,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildFilteredQuery(userId, positionCode, status, startDate, endDate).Count());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private IEnumerable<Interview> BuildFilteredQuery(
        Guid userId,
        string? positionCode,
        string? status,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var query = Interviews.Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            query = query.Where(x => x.PositionCode == positionCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (startDate.HasValue)
        {
            var start = startDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt <= end);
        }

        return query;
    }
}

sealed class InMemoryDashboardReportRepository : IReportRepository
{
    public List<InterviewReport> Reports { get; } = [];

    public List<InterviewScore> Scores { get; } = [];

    public Task<InterviewReport?> GetReportByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Reports.FirstOrDefault(x => x.InterviewId == interviewId));
    }

    public Task<InterviewScore?> GetScoreByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Scores.FirstOrDefault(x => x.InterviewId == interviewId));
    }

    public Task<Dictionary<Guid, InterviewScore>> GetScoresByInterviewIdsAsync(IEnumerable<Guid> interviewIds, CancellationToken cancellationToken = default)
    {
        var ids = interviewIds.ToHashSet();
        return Task.FromResult(Scores.Where(x => ids.Contains(x.InterviewId)).ToDictionary(x => x.InterviewId, x => x));
    }

    public Task<List<InterviewReport>> GetUserReportsAsync(Guid userId, string? positionCode, CancellationToken cancellationToken = default)
    {
        var query = Reports.Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            query = query.Where(x => x.PositionCode == positionCode);
        }

        return Task.FromResult(query.OrderBy(x => x.GeneratedAt).ToList());
    }

    public Task<RecommendationRecord?> GetLatestTrainingPlanAsync(Guid userId, Guid? interviewId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task AddOrUpdateScoreAsync(InterviewScore score, CancellationToken cancellationToken = default)
    {
        Scores.RemoveAll(x => x.InterviewId == score.InterviewId);
        Scores.Add(score);
        return Task.CompletedTask;
    }

    public Task AddOrUpdateReportAsync(InterviewReport report, CancellationToken cancellationToken = default)
    {
        Reports.RemoveAll(x => x.InterviewId == report.InterviewId);
        Reports.Add(report);
        return Task.CompletedTask;
    }

    public Task AddRecommendationRecordsAsync(IEnumerable<RecommendationRecord> records, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class DashboardStubAiProvider : IAiProvider
{
    public string ResponseText { get; set; } = "这是 AI 生成的个人画像总结。";

    public List<AiChatRequest> Requests { get; } = [];

    public Task<string> ChatCompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(ResponseText);
    }
}

sealed class DashboardStubAiSettingsService : IAiSettingsService
{
    public IAiProvider? Provider { get; set; }

    public Task<DTOs.Admin.AiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DTOs.Admin.AiSettingsDto
        {
            Provider = "openai_compatible",
            BaseUrl = "https://example.com/v1",
            Model = "test-model",
            Temperature = 0.2m,
            MaxTokens = 400,
            IsEnabled = Provider is not null,
            SystemPrompt = "test system prompt"
        });
    }

    public Task<DTOs.Admin.AiSettingsDto> UpdateSettingsAsync(DTOs.Admin.UpdateAiSettingsRequest request, string updatedBy, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<DTOs.Admin.AiTestResult> TestConnectionAsync(DTOs.Admin.TestAiConnectionRequest? request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IAiProvider?> BuildProviderAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Provider);
    }

    public Task<DTOs.Admin.AiRuntimeSettingsDto?> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DTOs.Admin.AiRuntimeSettingsDto?>(null);
    }
}

public class DashboardServiceTests
{
    [Fact]
    public async Task GetInsightsAsync_ShouldReturnAiGeneratedHeroSummary_WhenProviderIsAvailable()
    {
        var userId = Guid.NewGuid();
        var reportRepository = new InMemoryDashboardReportRepository();
        var interviewRepository = new InMemoryDashboardInterviewRepository();
        var aiProvider = new DashboardStubAiProvider
        {
            ResponseText = "AI 判断你目前表达稳定、结构清晰，但项目深挖仍需继续加强。"
        };

        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-01T08:00:00Z");
        SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-01T08:00:00Z",
            86m,
            strengths: ["回答结构清晰，能先给结论再展开说明。"],
            weaknesses: ["底层原理解释不够深入。"],
            suggestions: ["针对薄弱点补一轮底层原理梳理。"],
            dimensionScores: CreateDimensionScores(
                clarity: 90,
                fluency: 88,
                technicalAccuracy: 84,
                knowledgeDepth: 72,
                projectAuthenticity: 76,
                logicalThinking: 85,
                confidence: 80,
                positionMatch: 83));

        var service = CreateService(
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            interviewRepository,
            reportRepository,
            new DashboardStubAiSettingsService
            {
                Provider = aiProvider
            });

        var result = await service.GetInsightsAsync(userId);
        var heroSummaryProperty = result.GetType().GetProperty("HeroSummary");

        heroSummaryProperty.Should().NotBeNull();
        heroSummaryProperty!.GetValue(result).Should().Be("AI 判断你目前表达稳定、结构清晰，但项目深挖仍需继续加强。");
        aiProvider.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetInsightsAsync_ShouldUseAiAggregatedStrengthsAndWeaknesses_WhenProviderIsAvailable()
    {
        var userId = Guid.NewGuid();
        var reportRepository = new InMemoryDashboardReportRepository();
        var interviewRepository = new InMemoryDashboardInterviewRepository();
        var aiProvider = new DashboardStubAiProvider
        {
            ResponseText =
                """
                {
                  "heroSummary": "AI 判断你表达稳定，但项目深挖和底层原理还需要继续补强。",
                  "strengths": [
                    {
                      "title": "表达稳定",
                      "description": "你在最近几次面试里输出节奏稳定，能够比较自然地把核心观点讲清楚。",
                      "evidenceSamples": ["回答主线清楚，表达自然。", "沟通顺畅，信息传递完整。"],
                      "reportIndexes": [1, 2]
                    }
                  ],
                  "weaknesses": [
                    {
                      "title": "项目深挖不足",
                      "description": "你能讲清项目背景，但在技术细节、权衡和复盘上还不够深入。",
                      "typicalBehaviors": ["只讲做了什么，较少展开为什么这样做。", "谈到难点时细节支撑不足。"],
                      "suggestion": "把最近项目按“背景-目标-方案-难点-结果-反思”重讲一遍。",
                      "reportIndexes": [1]
                    }
                  ],
                  "nextActions": [
                    "把最近项目按“背景-目标-方案-难点-结果-反思”重讲一遍。",
                    "针对底层原理题补一轮机制和边界条件。"
                  ]
                }
                """
        };

        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-01T08:00:00Z");
        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-02T08:00:00Z");
        var reportA = SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-01T08:00:00Z",
            84m,
            strengths: ["回答结构清晰，能先给结论再展开说明。"],
            weaknesses: ["项目细节展开还不够深入。"],
            suggestions: ["把最近项目按背景-目标-方案-难点-结果-反思重讲一遍。"],
            dimensionScores: CreateDimensionScores(
                clarity: 88,
                fluency: 87,
                technicalAccuracy: 80,
                knowledgeDepth: 72,
                projectAuthenticity: 75,
                logicalThinking: 84,
                confidence: 81,
                positionMatch: 82));
        var reportB = SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-02T08:00:00Z",
            86m,
            strengths: ["表达自然，沟通顺畅。"],
            weaknesses: ["底层原理解释不够深入。"],
            suggestions: ["针对底层原理题补一轮机制和边界条件。"],
            dimensionScores: CreateDimensionScores(
                clarity: 90,
                fluency: 89,
                technicalAccuracy: 82,
                knowledgeDepth: 74,
                projectAuthenticity: 76,
                logicalThinking: 85,
                confidence: 82,
                positionMatch: 84));

        var service = CreateService(
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            interviewRepository,
            reportRepository,
            new DashboardStubAiSettingsService
            {
                Provider = aiProvider
            });

        var result = await service.GetInsightsAsync(userId);

        result.HeroSummary.Should().Be("AI 判断你表达稳定，但项目深挖和底层原理还需要继续补强。");
        result.Strengths.Should().ContainSingle();
        result.Strengths[0].Key.Should().Be("ai_strength_1");
        result.Strengths[0].Title.Should().Be("表达稳定");
        result.Strengths[0].EvidenceCount.Should().Be(2);
        result.Strengths[0].Sources.Select(x => x.ReportId).Should().Contain([reportA.Id, reportB.Id]);
        result.Strengths[0].LastSeenAt.Should().Be(reportB.GeneratedAt);

        result.Weaknesses.Should().ContainSingle();
        result.Weaknesses[0].Key.Should().Be("ai_weakness_1");
        result.Weaknesses[0].Title.Should().Be("项目深挖不足");
        result.Weaknesses[0].Suggestion.Should().Be("把最近项目按“背景-目标-方案-难点-结果-反思”重讲一遍。");
        result.Weaknesses[0].TypicalBehaviors.Should().Contain("只讲做了什么，较少展开为什么这样做。");
        result.Weaknesses[0].EvidenceCount.Should().Be(1);
        result.Weaknesses[0].Sources.Select(x => x.ReportId).Should().ContainSingle().Which.Should().Be(reportA.Id);
        result.NextActions.Should().ContainInOrder(
            "把最近项目按“背景-目标-方案-难点-结果-反思”重讲一遍。",
            "针对底层原理题补一轮机制和边界条件。");
    }

    [Fact]
    public async Task GetInsightsAsync_ShouldFallbackToRuleSummary_WhenProviderIsUnavailable()
    {
        var userId = Guid.NewGuid();
        var reportRepository = new InMemoryDashboardReportRepository();
        var interviewRepository = new InMemoryDashboardInterviewRepository();

        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-01T08:00:00Z");
        SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-01T08:00:00Z",
            86m,
            strengths: ["回答结构清晰，能先给结论再展开说明。"],
            weaknesses: ["底层原理解释不够深入。"],
            suggestions: ["针对薄弱点补一轮底层原理梳理。"],
            dimensionScores: CreateDimensionScores(
                clarity: 90,
                fluency: 88,
                technicalAccuracy: 84,
                knowledgeDepth: 72,
                projectAuthenticity: 76,
                logicalThinking: 85,
                confidence: 80,
                positionMatch: 83));

        var service = CreateService(
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            interviewRepository,
            reportRepository,
            new DashboardStubAiSettingsService());

        var result = await service.GetInsightsAsync(userId);
        var heroSummaryProperty = result.GetType().GetProperty("HeroSummary");
        var heroSummary = heroSummaryProperty?.GetValue(result) as string;

        heroSummaryProperty.Should().NotBeNull();
        heroSummary.Should().NotBeNullOrWhiteSpace();
        heroSummary.Should().Contain("回答结构化");
        heroSummary.Should().Contain("底层原理不够深入");
    }

    [Fact]
    public async Task GetInsightsAsync_ShouldPreferTargetPositionScope_WhenTargetReportsExist()
    {
        var userId = Guid.NewGuid();
        var reportRepository = new InMemoryDashboardReportRepository();
        var interviewRepository = new InMemoryDashboardInterviewRepository();

        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-01T08:00:00Z");
        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-02T08:00:00Z");
        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-03T08:00:00Z");
        SeedInterview(interviewRepository, userId, "java-backend", "2026-04-04T08:00:00Z");
        SeedInterview(interviewRepository, userId, "java-backend", "2026-04-05T08:00:00Z");

        var targetReportA = SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-01T08:00:00Z",
            86m,
            strengths:
            [
                "回答结构清晰，能先给结论再展开说明。",
                "表达节奏稳定，面试沟通感较好。"
            ],
            weaknesses:
            [
                "底层原理解释不够深入。",
                "关键取舍点可以再更明确。"
            ],
            suggestions:
            [
                "针对薄弱点补一轮底层原理梳理。",
                "把最近项目拆成 3 个 STAR 叙事模板。"
            ],
            dimensionScores: CreateDimensionScores(
                clarity: 90,
                fluency: 88,
                technicalAccuracy: 84,
                knowledgeDepth: 72,
                projectAuthenticity: 76,
                logicalThinking: 85,
                confidence: 80,
                positionMatch: 83));

        SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-03T08:00:00Z",
            82m,
            strengths:
            [
                "回答逻辑清晰，表达流畅。"
            ],
            weaknesses:
            [
                "底层原理解释不够深入。"
            ],
            suggestions:
            [
                "每周做 2 次限时口述训练。"
            ],
            dimensionScores: CreateDimensionScores(
                clarity: 84,
                fluency: 86,
                technicalAccuracy: 80,
                knowledgeDepth: 74,
                projectAuthenticity: 75,
                logicalThinking: 83,
                confidence: 79,
                positionMatch: 81));

        SeedReport(
            reportRepository,
            userId,
            "java-backend",
            "Java 后端开发工程师",
            "2026-04-04T08:00:00Z",
            95m,
            strengths:
            [
                "简历关键词与岗位需求存在一定关联（positionMatch 得分 30）"
            ],
            weaknesses:
            [
                "回答内容不可读，连续两轮追问均无法获取有效信息"
            ],
            suggestions:
            [
                "排查网络与输入法环境，确保信息传输无误"
            ],
            dimensionScores: CreateDimensionScores(
                clarity: 70,
                fluency: 72,
                technicalAccuracy: 93,
                knowledgeDepth: 90,
                projectAuthenticity: 92,
                logicalThinking: 88,
                confidence: 87,
                positionMatch: 95));

        var service = CreateService(userId, "web-frontend", "Web 前端开发工程师", interviewRepository, reportRepository);

        var result = await service.GetInsightsAsync(userId);

        result.Scope.ScopeStrategy.Should().Be("target_position_preferred_with_global_fallback");
        result.Scope.ActualScope.Should().Be("target_position");
        result.Scope.TargetPositionCode.Should().Be("web-frontend");
        result.Scope.TargetPositionName.Should().Be("Web 前端开发工程师");
        result.Scope.FallbackTriggered.Should().BeFalse();
        result.Scope.ReportCount.Should().Be(2);
        result.Overview.TotalReports.Should().Be(2);
        result.Overview.TotalInterviews.Should().Be(3);
        result.Strengths.Should().Contain(x => x.Key == "structured_answer");
        result.Weaknesses.Should().Contain(x => x.Key == "technical_depth");
        result.Strengths.Should().NotContain(x => x.Key == "position_fit");
        result.RecentTrend.Should().OnlyContain(x => x.ReportId != Guid.Empty);
        result.RecentTrend.Select(x => x.ReportId).Should().Contain(targetReportA.Id);
    }

    [Fact]
    public async Task GetInsightsAsync_ShouldFallbackToAllReports_WhenTargetPositionHasNoReports()
    {
        var userId = Guid.NewGuid();
        var reportRepository = new InMemoryDashboardReportRepository();
        var interviewRepository = new InMemoryDashboardInterviewRepository();

        SeedInterview(interviewRepository, userId, "java-backend", "2026-04-01T08:00:00Z");
        SeedInterview(interviewRepository, userId, "java-backend", "2026-04-02T08:00:00Z");

        SeedReport(
            reportRepository,
            userId,
            "java-backend",
            "Java 后端开发工程师",
            "2026-04-02T08:00:00Z",
            88m,
            strengths:
            [
                "回答结构清晰，能先给结论再展开说明。"
            ],
            weaknesses:
            [
                "项目深挖不足，缺少技术细节。"
            ],
            suggestions:
            [
                "把最近项目拆成 3 个 STAR 叙事模板。"
            ],
            dimensionScores: CreateDimensionScores(
                clarity: 84,
                fluency: 86,
                technicalAccuracy: 90,
                knowledgeDepth: 78,
                projectAuthenticity: 80,
                logicalThinking: 83,
                confidence: 82,
                positionMatch: 87));

        var service = CreateService(userId, "web-frontend", "Web 前端开发工程师", interviewRepository, reportRepository);

        var result = await service.GetInsightsAsync(userId);

        result.Scope.ActualScope.Should().Be("all_reports");
        result.Scope.FallbackTriggered.Should().BeTrue();
        result.Scope.FallbackReason.Should().Be("target_position_has_no_reports");
        result.Scope.ReportCount.Should().Be(1);
        result.Overview.TotalInterviews.Should().Be(2);
        result.AbilityDimensions6.Should().ContainSingle(x => x.Key == "expression")
            .Which.SourceDimensions.Should().Equal(["clarity", "fluency"]);
        result.AbilityDimensions6.Should().ContainSingle(x => x.Key == "project_depth")
            .Which.Score.Should().Be(79);
    }

    [Fact]
    public async Task GetInsightsAsync_ShouldBuildRecentTrend_WithReportScoreThenDimensionAverageThenSkip()
    {
        var userId = Guid.NewGuid();
        var reportRepository = new InMemoryDashboardReportRepository();
        var interviewRepository = new InMemoryDashboardInterviewRepository();

        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-01T08:00:00Z");
        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-02T08:00:00Z");
        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-03T08:00:00Z");

        var reportA = SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-01T08:00:00Z",
            81m,
            strengths: ["回答结构清晰，能先给结论再展开说明。"],
            weaknesses: ["底层原理解释不够深入。"],
            suggestions: ["针对薄弱点补一轮底层原理梳理。"],
            dimensionScores: CreateDimensionScores(
                clarity: 82,
                fluency: 84,
                technicalAccuracy: 80,
                knowledgeDepth: 76,
                projectAuthenticity: 75,
                logicalThinking: 83,
                confidence: 78,
                positionMatch: 80));

        var reportB = SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-02T08:00:00Z",
            0m,
            strengths: ["表达节奏稳定，面试沟通感较好。"],
            weaknesses: ["关键取舍点可以再更明确。"],
            suggestions: ["把最近项目拆成 3 个 STAR 叙事模板。"],
            dimensionScores: CreateDimensionScores(
                clarity: 70,
                fluency: 72,
                technicalAccuracy: 68,
                knowledgeDepth: 66,
                projectAuthenticity: 64,
                logicalThinking: 74,
                confidence: 76,
                positionMatch: 70));

        SeedReport(
            reportRepository,
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            "2026-04-03T08:00:00Z",
            0m,
            strengths: ["回答结构清晰，能先给结论再展开说明。"],
            weaknesses: ["底层原理解释不够深入。"],
            suggestions: ["每周做 2 次限时口述训练。"],
            dimensionScores: []);

        var service = CreateService(userId, "web-frontend", "Web 前端开发工程师", interviewRepository, reportRepository);

        var result = await service.GetInsightsAsync(userId);

        result.RecentTrend.Select(x => x.ReportId).Should().Equal(reportA.Id, reportB.Id);
        result.RecentTrend.Select(x => x.Score).Should().Equal(81m, 70m);
        result.RecentTrend.Should().BeInAscendingOrder(x => x.Date);
    }

    [Fact]
    public async Task GetInsightsAsync_ShouldAggregateTaxonomy_ByReportAndHandleUnreadableResponses()
    {
        var userId = Guid.NewGuid();
        var reportRepository = new InMemoryDashboardReportRepository();
        var interviewRepository = new InMemoryDashboardInterviewRepository();

        SeedInterview(interviewRepository, userId, "java-backend", "2026-04-01T08:00:00Z");
        SeedInterview(interviewRepository, userId, "java-backend", "2026-04-02T08:00:00Z");
        SeedInterview(interviewRepository, userId, "java-backend", "2026-04-03T08:00:00Z");

        SeedReport(
            reportRepository,
            userId,
            "java-backend",
            "Java 后端开发工程师",
            "2026-04-01T08:00:00Z",
            78m,
            strengths:
            [
                "表达清晰，沟通自然。",
                "语言流畅，叙述完整。"
            ],
            weaknesses:
            [
                "项目深挖不足，缺少技术细节。",
                "缺少难点分析。"
            ],
            suggestions:
            [
                "按“背景-目标-方案-难点-结果-反思”模板重构项目回答。"
            ],
            executiveSummary: "整体表达清晰，但项目深度仍需继续加强。",
            dimensionScores: CreateDimensionScores(
                clarity: 80,
                fluency: 82,
                technicalAccuracy: 79,
                knowledgeDepth: 68,
                projectAuthenticity: 66,
                logicalThinking: 75,
                confidence: 74,
                positionMatch: 77));

        SeedReport(
            reportRepository,
            userId,
            "java-backend",
            "Java 后端开发工程师",
            "2026-04-02T08:00:00Z",
            74m,
            strengths:
            [
                "沟通自然，语言流畅。"
            ],
            weaknesses:
            [
                "回答内容不可读，连续两轮追问均无法获取有效信息"
            ],
            suggestions:
            [
                "排查网络与输入法环境，确保信息传输无误"
            ],
            dimensionScores: CreateDimensionScores(
                clarity: 65,
                fluency: 66,
                technicalAccuracy: 70,
                knowledgeDepth: 69,
                projectAuthenticity: 68,
                logicalThinking: 62,
                confidence: 60,
                positionMatch: 71));

        var service = CreateService(userId, "java-backend", "Java 后端开发工程师", interviewRepository, reportRepository);

        var result = await service.GetInsightsAsync(userId);

        result.Strengths.Should().ContainSingle(x => x.Key == "clear_expression")
            .Which.EvidenceCount.Should().Be(2);
        result.Weaknesses.Should().ContainSingle(x => x.Key == "project_depth")
            .Which.EvidenceCount.Should().Be(1);
        result.Weaknesses.Should().ContainSingle(x => x.Key == "response_validity")
            .Which.Suggestion.Should().Contain("排查网络与输入法环境");
        result.NextActions.Should().Contain(x => x.Contains("项目回答", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetInsightsAsync_ShouldReturnEmptyStructure_WhenNoReportsExist()
    {
        var userId = Guid.NewGuid();
        var interviewRepository = new InMemoryDashboardInterviewRepository();
        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-01T08:00:00Z");
        SeedInterview(interviewRepository, userId, "web-frontend", "2026-04-02T08:00:00Z");

        var service = CreateService(
            userId,
            "web-frontend",
            "Web 前端开发工程师",
            interviewRepository,
            new InMemoryDashboardReportRepository());

        var result = await service.GetInsightsAsync(userId);

        result.Overview.TotalReports.Should().Be(0);
        result.Overview.TotalInterviews.Should().Be(2);
        result.Scope.ReportCount.Should().Be(0);
        result.Strengths.Should().BeEmpty();
        result.Weaknesses.Should().BeEmpty();
        result.AbilityDimensions6.Should().BeEmpty();
        result.RecentTrend.Should().BeEmpty();
        result.NextActions.Should().BeEmpty();
    }

    private static DashboardService CreateService(
        Guid userId,
        string? targetPositionCode,
        string? targetPositionName,
        InMemoryDashboardInterviewRepository interviewRepository,
        InMemoryDashboardReportRepository reportRepository,
        DashboardStubAiSettingsService? aiSettingsService = null)
    {
        var userRepository = new InMemoryDashboardUserRepository
        {
            User = new User
            {
                Id = userId,
                Username = "zhangsan",
                Email = "zhangsan@example.com",
                TargetPositionCode = targetPositionCode,
                TargetPosition = targetPositionCode is null
                    ? null
                    : new Position
                    {
                        Code = targetPositionCode,
                        Name = targetPositionName ?? targetPositionCode
                    }
            }
        };

        var constructor = typeof(DashboardService).GetConstructors().Single();
        var parameters = constructor.GetParameters();

        return parameters.Length switch
        {
            3 => (DashboardService)constructor.Invoke(
                [
                    userRepository,
                    interviewRepository,
                    reportRepository
                ]),
            5 => (DashboardService)constructor.Invoke(
                [
                    userRepository,
                    interviewRepository,
                    reportRepository,
                    aiSettingsService ?? new DashboardStubAiSettingsService(),
                    NullLogger<DashboardService>.Instance
                ]),
            _ => throw new NotSupportedException($"Unexpected DashboardService constructor parameter count: {parameters.Length}")
        };
    }

    private static void SeedInterview(
        InMemoryDashboardInterviewRepository interviewRepository,
        Guid userId,
        string positionCode,
        string createdAt)
    {
        interviewRepository.Interviews.Add(new Interview
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PositionCode = positionCode,
            Status = "completed",
            InterviewMode = "standard",
            CreatedAt = DateTimeOffset.Parse(createdAt),
            TotalRounds = 3,
            CurrentRound = 3
        });
    }

    private static InterviewReport SeedReport(
        InMemoryDashboardReportRepository reportRepository,
        Guid userId,
        string positionCode,
        string positionName,
        string generatedAt,
        decimal overallScore,
        string[] strengths,
        string[] weaknesses,
        string[] suggestions,
        Dictionary<string, DimensionScoreDto> dimensionScores,
        string? executiveSummary = null)
    {
        var interviewId = Guid.NewGuid();
        var report = new InterviewReport
        {
            Id = Guid.NewGuid(),
            InterviewId = interviewId,
            UserId = userId,
            PositionCode = positionCode,
            OverallScore = overallScore,
            ExecutiveSummary = executiveSummary,
            Strengths = strengths,
            Weaknesses = weaknesses,
            LearningSuggestions = suggestions,
            GeneratedAt = DateTimeOffset.Parse(generatedAt),
            Position = new Position
            {
                Code = positionCode,
                Name = positionName
            }
        };
        reportRepository.Reports.Add(report);
        reportRepository.Scores.Add(new InterviewScore
        {
            Id = Guid.NewGuid(),
            InterviewId = interviewId,
            OverallScore = overallScore,
            DimensionScores = JsonSerializer.Serialize(dimensionScores)
        });
        return report;
    }

    private static Dictionary<string, DimensionScoreDto> CreateDimensionScores(
        decimal clarity,
        decimal fluency,
        decimal technicalAccuracy,
        decimal knowledgeDepth,
        decimal projectAuthenticity,
        decimal logicalThinking,
        decimal confidence,
        decimal positionMatch)
    {
        return new Dictionary<string, DimensionScoreDto>
        {
            ["clarity"] = new() { Score = clarity, Weight = 0.03m },
            ["fluency"] = new() { Score = fluency, Weight = 0.05m },
            ["technicalAccuracy"] = new() { Score = technicalAccuracy, Weight = 0.30m },
            ["knowledgeDepth"] = new() { Score = knowledgeDepth, Weight = 0.20m },
            ["projectAuthenticity"] = new() { Score = projectAuthenticity, Weight = 0.10m },
            ["logicalThinking"] = new() { Score = logicalThinking, Weight = 0.15m },
            ["confidence"] = new() { Score = confidence, Weight = 0.02m },
            ["positionMatch"] = new() { Score = positionMatch, Weight = 0.15m }
        };
    }
}

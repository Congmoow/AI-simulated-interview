using AiInterview.Api.Hubs;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services;
using AiInterview.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Api.Tests.Services;

file sealed class InMemoryInterviewRepository : IInterviewRepository
{
    public Interview? Interview { get; set; }
    public int SaveChangesCallCount { get; private set; }

    public Task AddInterviewAsync(Interview interview, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddRoundAsync(InterviewRound round, CancellationToken cancellationToken = default)
    {
        Interview?.Rounds.Add(round);
        return Task.CompletedTask;
    }

    public Task<Interview?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Interview?.Id == id ? Interview : null);
    }

    public Task<Interview?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Interview?.Id == id ? Interview : null);
    }

    public Task<List<Interview>> GetUserHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<int> CountUserHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount += 1;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryReportRepository : IReportRepository
{
    public InterviewScore? SavedScore { get; private set; }
    public InterviewReport? SavedReport { get; private set; }
    public int RecommendationRecordCallCount { get; private set; }

    public Task<InterviewReport?> GetReportByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SavedReport?.InterviewId == interviewId ? SavedReport : null);
    }

    public Task<InterviewScore?> GetScoreByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SavedScore?.InterviewId == interviewId ? SavedScore : null);
    }

    public Task<List<InterviewReport>> GetUserReportsAsync(Guid userId, string? positionCode, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<RecommendationRecord?> GetLatestTrainingPlanAsync(Guid userId, Guid? interviewId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task AddOrUpdateScoreAsync(InterviewScore score, CancellationToken cancellationToken = default)
    {
        SavedScore = score;
        return Task.CompletedTask;
    }

    public Task AddOrUpdateReportAsync(InterviewReport report, CancellationToken cancellationToken = default)
    {
        SavedReport = report;
        return Task.CompletedTask;
    }

    public Task AddRecommendationRecordsAsync(IEnumerable<RecommendationRecord> records, CancellationToken cancellationToken = default)
    {
        RecommendationRecordCallCount += 1;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class StubCatalogRepository : ICatalogRepository
{
    public int ResourceLookupCallCount { get; private set; }

    public Task<List<Position>> GetActivePositionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Position?> GetPositionByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<List<QuestionBank>> GetQuestionsAsync(string? positionCode, string? type, string? difficulty, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> CountQuestionsAsync(string? positionCode, string? type, string? difficulty, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<QuestionBank?> GetRandomQuestionAsync(string positionCode, IEnumerable<string> questionTypes, IEnumerable<Guid> excludedQuestionIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<List<LearningResource>> GetLearningResourcesAsync(string? positionCode, IEnumerable<string>? dimensions, int limit, CancellationToken cancellationToken = default)
    {
        ResourceLookupCallCount += 1;
        return Task.FromResult(new List<LearningResource>());
    }

    public Task<int> CountQuestionsByPositionAsync(string positionCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Dictionary<string, int>> GetQuestionTypeCountsAsync(string positionCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

file sealed class StubAiIntegrationService : IAiIntegrationService
{
    public int FinishCallCount { get; private set; }
    public int ScoreCallCount { get; private set; }
    public int GenerateReportCallCount { get; private set; }
    public int RecommendResourcesCallCount { get; private set; }
    public int GenerateTrainingPlanCallCount { get; private set; }
    public ReportAiRequest? LastReportRequest { get; private set; }

    public Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<FinishInterviewAiResponse> FinishInterviewAsync(FinishInterviewAiRequest request, CancellationToken cancellationToken = default)
    {
        FinishCallCount += 1;
        return Task.FromResult(new FinishInterviewAiResponse { Summary = "完成" });
    }

    public Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default)
    {
        ScoreCallCount += 1;
        return Task.FromResult(new ScoreAiResponse
        {
            OverallScore = 81.5m,
            RankPercentile = 86,
            ModelVersion = "qwen:qwen-plus",
            DimensionScores = new Dictionary<string, DimensionScoreDto>
            {
                ["technicalAccuracy"] = new() { Score = 80, Weight = 0.3m },
                ["knowledgeDepth"] = new() { Score = 78, Weight = 0.2m },
            },
            DimensionDetails = new Dictionary<string, string>
            {
                ["technicalAccuracy"] = "扎实"
            },
            ScoreBreakdown = new Dictionary<string, object>()
        });
    }

    public Task<ReportAiResponse> GenerateReportAsync(ReportAiRequest request, CancellationToken cancellationToken = default)
    {
        GenerateReportCallCount += 1;
        LastReportRequest = request;
        return Task.FromResult(new ReportAiResponse
        {
            ExecutiveSummary = "总结",
            Strengths = ["优点"],
            Weaknesses = ["不足"],
            DetailedAnalysis = new Dictionary<string, object> { ["technicalAccuracy"] = "分析" },
            LearningSuggestions = ["建议"],
            TrainingPlan = [],
            NextInterviewFocus = ["项目深挖"],
            ModelVersion = "qwen:qwen-plus"
        });
    }

    public Task<TrainingPlanAiResponse> GenerateTrainingPlanAsync(TrainingPlanAiRequest request, CancellationToken cancellationToken = default)
    {
        GenerateTrainingPlanCallCount += 1;
        return Task.FromResult(new TrainingPlanAiResponse
        {
            Weeks = 4,
            DailyCommitment = "2小时",
            Goals = ["补强"],
            Schedule = [],
            Milestones = []
        });
    }

    public Task<ProcessDocumentAiResponse> ProcessDocumentAsync(ProcessDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<EnqueueDocumentAiResponse> EnqueueDocumentAsync(EnqueueDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ResourceRecommendationAiResponse> RecommendResourcesAsync(ResourceRecommendationAiRequest request, CancellationToken cancellationToken = default)
    {
        RecommendResourcesCallCount += 1;
        return Task.FromResult(new ResourceRecommendationAiResponse
        {
            TargetDimensions = ["technicalAccuracy"],
            MatchScores = new Dictionary<string, decimal> { ["technicalAccuracy"] = 0.91m },
            Reason = "test"
        });
    }
}

file sealed class StubAiSettingsService : IAiSettingsService
{
    public Task<AiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AiSettingsDto> UpdateSettingsAsync(UpdateAiSettingsRequest request, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AiTestResult> TestConnectionAsync(TestAiConnectionRequest? request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IAiProvider?> BuildProviderAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IAiProvider?>(null);
    }

    public Task<AiRuntimeSettingsDto?> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AiRuntimeSettingsDto?>(null);
    }
}

file sealed class NoopInterviewClient : IInterviewClient
{
    public Task ReceiveQuestion(object payload) => Task.CompletedTask;
    public Task ReceiveFollowUp(object payload) => Task.CompletedTask;
    public Task TypingIndicator(object payload) => Task.CompletedTask;
    public Task InterviewStatusChanged(object payload) => Task.CompletedTask;
    public Task ReportProgress(object payload) => Task.CompletedTask;
    public Task ReportReady(object payload) => Task.CompletedTask;
    public Task VoiceTranscription(object payload) => Task.CompletedTask;
    public Task ErrorOccurred(object payload) => Task.CompletedTask;
}

file sealed class StubHubClients : IHubClients<IInterviewClient>
{
    private static readonly IInterviewClient SharedClient = new NoopInterviewClient();

    public IInterviewClient All => SharedClient;
    public IInterviewClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => SharedClient;
    public IInterviewClient Client(string connectionId) => SharedClient;
    public IInterviewClient Clients(IReadOnlyList<string> connectionIds) => SharedClient;
    public IInterviewClient Group(string groupName) => SharedClient;
    public IInterviewClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => SharedClient;
    public IInterviewClient Groups(IReadOnlyList<string> groupNames) => SharedClient;
    public IInterviewClient User(string userId) => SharedClient;
    public IInterviewClient Users(IReadOnlyList<string> userIds) => SharedClient;
}

file sealed class StubGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class StubHubContext : IHubContext<InterviewHub, IInterviewClient>
{
    public IHubClients<IInterviewClient> Clients { get; } = new StubHubClients();

    public IGroupManager Groups { get; } = new StubGroupManager();
}

public class InterviewServiceTests
{
    [Fact]
    public async Task FinishInterviewAsync_ShouldNotCallRecommendationOrTrainingPlan_AndShouldSendRoundsToReport()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = new Interview
            {
                Id = interviewId,
                UserId = userId,
                PositionCode = "java-backend",
                InterviewMode = "standard",
                Status = "in_progress",
                TotalRounds = 3,
                CurrentRound = 3,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-18),
                Rounds =
                [
                    new InterviewRound
                    {
                        InterviewId = interviewId,
                        RoundNumber = 1,
                        QuestionId = Guid.NewGuid(),
                        QuestionTitle = "介绍项目",
                        QuestionType = "project",
                        QuestionContent = "请介绍订单系统",
                        UserAnswer = "我负责下单链路和库存一致性。",
                        AiFollowUps = ["请继续说明压测结果。"]
                    },
                    new InterviewRound
                    {
                        InterviewId = interviewId,
                        RoundNumber = 2,
                        QuestionId = Guid.NewGuid(),
                        QuestionTitle = "Spring 事务",
                        QuestionType = "knowledge",
                        QuestionContent = "事务传播怎么选",
                        UserAnswer = "我会结合边界与回滚要求选择。",
                        AiFollowUps = []
                    }
                ]
            }
        };
        var reportRepository = new InMemoryReportRepository();
        var catalogRepository = new StubCatalogRepository();
        var aiIntegrationService = new StubAiIntegrationService();
        var service = new InterviewService(
            interviewRepository,
            catalogRepository,
            reportRepository,
            aiIntegrationService,
            new StubAiSettingsService(),
            new StubHubContext(),
            NullLogger<InterviewService>.Instance);

        var result = await service.FinishInterviewAsync(userId, interviewId);

        result.ReportId.Should().NotBeEmpty();
        aiIntegrationService.FinishCallCount.Should().Be(1);
        aiIntegrationService.ScoreCallCount.Should().Be(1);
        aiIntegrationService.GenerateReportCallCount.Should().Be(1);
        aiIntegrationService.RecommendResourcesCallCount.Should().Be(0);
        aiIntegrationService.GenerateTrainingPlanCallCount.Should().Be(0);
        reportRepository.RecommendationRecordCallCount.Should().Be(0);
        catalogRepository.ResourceLookupCallCount.Should().Be(0);
        aiIntegrationService.LastReportRequest.Should().NotBeNull();
        aiIntegrationService.LastReportRequest!.Rounds.Should().HaveCount(2);
        aiIntegrationService.LastReportRequest.Rounds[0].QuestionTitle.Should().Be("介绍项目");
        aiIntegrationService.LastReportRequest.Rounds[0].FollowUps.Should().ContainSingle().Which.Should().Be("请继续说明压测结果。");
    }
}

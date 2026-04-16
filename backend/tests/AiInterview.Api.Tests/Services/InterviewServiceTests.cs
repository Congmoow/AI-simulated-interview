using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Hubs;
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

    public List<Guid> PendingGeneratingReportInterviewIds { get; set; } = [];

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
        throw new NotSupportedException();
    }

    public Task<int> CountUserHistoryAsync(
        Guid userId,
        string? positionCode,
        string? status,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<List<Guid>> GetInterviewIdsPendingReportGenerationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PendingGeneratingReportInterviewIds);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount += 1;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryReportRepository : IReportRepository
{
    public InterviewScore? SavedScore { get; set; }

    public InterviewReport? SavedReport { get; set; }

    public int SaveChangesCallCount { get; private set; }

    public int RecommendationRecordCallCount { get; private set; }

    public Task<InterviewReport?> GetReportByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SavedReport?.InterviewId == interviewId ? SavedReport : null);
    }

    public Task<InterviewScore?> GetScoreByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SavedScore?.InterviewId == interviewId ? SavedScore : null);
    }

    public Task<Dictionary<Guid, InterviewScore>> GetScoresByInterviewIdsAsync(IEnumerable<Guid> interviewIds, CancellationToken cancellationToken = default)
    {
        var result = SavedScore is not null && interviewIds.Contains(SavedScore.InterviewId)
            ? new Dictionary<Guid, InterviewScore> { [SavedScore.InterviewId] = SavedScore }
            : [];
        return Task.FromResult(result);
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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount += 1;
        return Task.CompletedTask;
    }
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

file sealed class StubInterviewReportGenerationQueue : IInterviewReportGenerationQueue
{
    private readonly HashSet<Guid> _queuedIds = [];

    public List<Guid> EnqueuedInterviewIds { get; } = [];

    public ValueTask<bool> EnqueueAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        if (!_queuedIds.Add(interviewId))
        {
            return ValueTask.FromResult(false);
        }

        EnqueuedInterviewIds.Add(interviewId);
        return ValueTask.FromResult(true);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public bool IsQueued(Guid interviewId)
    {
        return _queuedIds.Contains(interviewId);
    }

    public void MarkCompleted(Guid interviewId)
    {
        _queuedIds.Remove(interviewId);
    }

    public void Seed(Guid interviewId)
    {
        _queuedIds.Add(interviewId);
    }
}

file sealed class StubAiProvider : IAiProvider
{
    public bool ShouldThrow { get; set; }

    public List<AiChatRequest> Requests { get; } = [];

    public Task<string> ChatCompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (ShouldThrow)
        {
            throw new InvalidOperationException("provider failed");
        }

        return Task.FromResult("""
            {
              "overallScore": 82,
              "dimensions": {
                "technicalAccuracy": {
                  "score": 80,
                  "detail": "stable"
                }
              },
              "strengths": ["clear structure"],
              "weaknesses": ["more depth needed"],
              "suggestions": ["add metrics"],
              "summary": "generated by direct provider"
            }
            """);
    }
}

file sealed class StubAiIntegrationService : IAiIntegrationService
{
    public int ScoreCallCount { get; private set; }

    public int GenerateReportCallCount { get; private set; }

    public int RecommendResourcesCallCount { get; private set; }

    public int GenerateTrainingPlanCallCount { get; private set; }

    public ReportAiRequest? LastReportRequest { get; private set; }

    public ScoreAiRequest? LastScoreRequest { get; private set; }

    public Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default)
    {
        ScoreCallCount += 1;
        LastScoreRequest = request;
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
                ["technicalAccuracy"] = "solid",
                ["knowledgeDepth"] = "could go deeper"
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
            ExecutiveSummary = "fallback summary",
            Strengths = ["strength"],
            Weaknesses = ["weakness"],
            DetailedAnalysis = new Dictionary<string, object> { ["technicalAccuracy"] = "analysis" },
            LearningSuggestions = ["suggestion"],
            TrainingPlan = [],
            NextInterviewFocus = ["focus"],
            ModelVersion = "fallback-model"
        });
    }

    public Task<TrainingPlanAiResponse> GenerateTrainingPlanAsync(TrainingPlanAiRequest request, CancellationToken cancellationToken = default)
    {
        GenerateTrainingPlanCallCount += 1;
        return Task.FromResult(new TrainingPlanAiResponse());
    }

    public Task<ResourceRecommendationAiResponse> RecommendResourcesAsync(ResourceRecommendationAiRequest request, CancellationToken cancellationToken = default)
    {
        RecommendResourcesCallCount += 1;
        return Task.FromResult(new ResourceRecommendationAiResponse());
    }

    public Task<ProcessDocumentAiResponse> ProcessDocumentAsync(ProcessDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<EnqueueDocumentAiResponse> EnqueueDocumentAsync(EnqueueDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

file sealed class StubAiSettingsService : IAiSettingsService
{
    public IAiProvider? Provider { get; set; }

    public Task<AiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AiSettingsDto
        {
            Provider = "openai_compatible",
            BaseUrl = "https://example.com/v1",
            Model = "test-model",
            Temperature = 0.2m,
            MaxTokens = 1200,
            IsEnabled = Provider is not null,
            SystemPrompt = "test system prompt"
        });
    }

    public Task<AiSettingsDto> UpdateSettingsAsync(UpdateAiSettingsRequest request, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AiTestResult> TestConnectionAsync(TestAiConnectionRequest? request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IAiProvider?> BuildProviderAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Provider);
    }

    public Task<AiRuntimeSettingsDto?> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AiRuntimeSettingsDto?>(null);
    }
}

file sealed class CapturingInterviewClient : IInterviewClient
{
    public List<object> InterviewStatusChangedPayloads { get; } = [];

    public List<object> ReportProgressPayloads { get; } = [];

    public List<object> ReportReadyPayloads { get; } = [];

    public List<object> ErrorPayloads { get; } = [];

    public Task ReceiveQuestion(object payload) => Task.CompletedTask;

    public Task ReceiveFollowUp(object payload) => Task.CompletedTask;

    public Task TypingIndicator(object payload) => Task.CompletedTask;

    public Task InterviewStatusChanged(object payload)
    {
        InterviewStatusChangedPayloads.Add(payload);
        return Task.CompletedTask;
    }

    public Task ReportProgress(object payload)
    {
        ReportProgressPayloads.Add(payload);
        return Task.CompletedTask;
    }

    public Task ReportReady(object payload)
    {
        ReportReadyPayloads.Add(payload);
        return Task.CompletedTask;
    }

    public Task VoiceTranscription(object payload) => Task.CompletedTask;

    public Task ErrorOccurred(object payload)
    {
        ErrorPayloads.Add(payload);
        return Task.CompletedTask;
    }
}

file sealed class StubHubClients : IHubClients<IInterviewClient>
{
    public CapturingInterviewClient SharedClient { get; } = new();

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
    public StubHubClients HubClients { get; } = new();

    public IHubClients<IInterviewClient> Clients => HubClients;

    public IGroupManager Groups { get; } = new StubGroupManager();
}

public class InterviewServiceTests
{
    [Fact]
    public async Task FinishInterviewAsync_ShouldEnqueueReportGenerationAndReturnImmediately()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = BuildInterview(userId, interviewId, InterviewStatuses.InProgress)
        };
        var reportRepository = new InMemoryReportRepository();
        var catalogRepository = new StubCatalogRepository();
        var aiIntegrationService = new StubAiIntegrationService();
        var queue = new StubInterviewReportGenerationQueue();
        var service = new InterviewService(
            interviewRepository,
            catalogRepository,
            reportRepository,
            aiIntegrationService,
            new StubAiSettingsService(),
            queue,
            new StubHubContext(),
            NullLogger<InterviewService>.Instance);

        var result = await service.FinishInterviewAsync(userId, interviewId);

        result.InterviewId.Should().Be(interviewId);
        result.Status.Should().Be(InterviewStatuses.GeneratingReport);
        result.ReportId.Should().BeNull();
        queue.EnqueuedInterviewIds.Should().ContainSingle().Which.Should().Be(interviewId);
        interviewRepository.Interview!.Status.Should().Be(InterviewStatuses.GeneratingReport);
        interviewRepository.SaveChangesCallCount.Should().Be(1);
        aiIntegrationService.ScoreCallCount.Should().Be(0);
        aiIntegrationService.GenerateReportCallCount.Should().Be(0);
        aiIntegrationService.RecommendResourcesCallCount.Should().Be(0);
        aiIntegrationService.GenerateTrainingPlanCallCount.Should().Be(0);
        reportRepository.RecommendationRecordCallCount.Should().Be(0);
        catalogRepository.ResourceLookupCallCount.Should().Be(0);
        aiIntegrationService.LastReportRequest.Should().BeNull();
    }

    [Fact]
    public async Task FinishInterviewAsync_ShouldReturnExistingReport_WhenReportAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = new Interview
            {
                Id = interviewId,
                UserId = userId,
                PositionCode = "java-backend",
                Status = InterviewStatuses.Completed,
                Report = new InterviewReport
                {
                    Id = reportId,
                    InterviewId = interviewId,
                    UserId = userId,
                    PositionCode = "java-backend",
                    OverallScore = 86
                }
            }
        };
        var queue = new StubInterviewReportGenerationQueue();
        var service = new InterviewService(
            interviewRepository,
            new StubCatalogRepository(),
            new InMemoryReportRepository(),
            new StubAiIntegrationService(),
            new StubAiSettingsService(),
            queue,
            new StubHubContext(),
            NullLogger<InterviewService>.Instance);

        var result = await service.FinishInterviewAsync(userId, interviewId);

        result.Status.Should().Be(InterviewStatuses.Completed);
        result.ReportId.Should().Be(reportId);
        queue.EnqueuedInterviewIds.Should().BeEmpty();
    }

    [Fact]
    public async Task FinishInterviewAsync_ShouldNotDuplicateQueue_WhenInterviewAlreadyGeneratingReport()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = BuildInterview(userId, interviewId, InterviewStatuses.GeneratingReport)
        };
        var queue = new StubInterviewReportGenerationQueue();
        queue.Seed(interviewId);
        var service = new InterviewService(
            interviewRepository,
            new StubCatalogRepository(),
            new InMemoryReportRepository(),
            new StubAiIntegrationService(),
            new StubAiSettingsService(),
            queue,
            new StubHubContext(),
            NullLogger<InterviewService>.Instance);

        var result = await service.FinishInterviewAsync(userId, interviewId);

        result.Status.Should().Be(InterviewStatuses.GeneratingReport);
        result.ReportId.Should().BeNull();
        queue.EnqueuedInterviewIds.Should().BeEmpty();
    }

    [Fact]
    public async Task FinishInterviewAsync_ShouldRetry_WhenLastGenerationFailed()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = BuildInterview(userId, interviewId, InterviewStatuses.ReportFailed)
        };
        var queue = new StubInterviewReportGenerationQueue();
        var service = new InterviewService(
            interviewRepository,
            new StubCatalogRepository(),
            new InMemoryReportRepository(),
            new StubAiIntegrationService(),
            new StubAiSettingsService(),
            queue,
            new StubHubContext(),
            NullLogger<InterviewService>.Instance);

        var result = await service.FinishInterviewAsync(userId, interviewId);

        result.Status.Should().Be(InterviewStatuses.GeneratingReport);
        result.ReportId.Should().BeNull();
        interviewRepository.Interview!.Status.Should().Be(InterviewStatuses.GeneratingReport);
        queue.EnqueuedInterviewIds.Should().ContainSingle().Which.Should().Be(interviewId);
    }

    [Fact]
    public async Task ProcessInterviewAsync_ShouldGenerateScoreAndUseDirectProviderBeforeFallback()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = BuildInterview(userId, interviewId, InterviewStatuses.GeneratingReport)
        };
        var reportRepository = new InMemoryReportRepository();
        var aiIntegrationService = new StubAiIntegrationService();
        var aiSettingsService = new StubAiSettingsService
        {
            Provider = new StubAiProvider()
        };
        var hubContext = new StubHubContext();
        var service = new InterviewReportGenerationService(
            interviewRepository,
            reportRepository,
            aiIntegrationService,
            aiSettingsService,
            hubContext,
            NullLogger<InterviewReportGenerationService>.Instance);

        await service.ProcessInterviewAsync(interviewId);

        aiIntegrationService.ScoreCallCount.Should().Be(1);
        aiIntegrationService.GenerateReportCallCount.Should().Be(0);
        reportRepository.SavedScore.Should().NotBeNull();
        reportRepository.SavedReport.Should().NotBeNull();
        reportRepository.SavedReport!.ExecutiveSummary.Should().Be("generated by direct provider");
        interviewRepository.Interview!.Status.Should().Be(InterviewStatuses.Completed);
        hubContext.HubClients.SharedClient.ReportReadyPayloads.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessInterviewAsync_ShouldFallbackToAiService_WhenDirectProviderFails()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = BuildInterview(userId, interviewId, InterviewStatuses.GeneratingReport)
        };
        var reportRepository = new InMemoryReportRepository();
        var aiIntegrationService = new StubAiIntegrationService();
        var aiSettingsService = new StubAiSettingsService
        {
            Provider = new StubAiProvider { ShouldThrow = true }
        };
        var hubContext = new StubHubContext();
        var service = new InterviewReportGenerationService(
            interviewRepository,
            reportRepository,
            aiIntegrationService,
            aiSettingsService,
            hubContext,
            NullLogger<InterviewReportGenerationService>.Instance);

        await service.ProcessInterviewAsync(interviewId);

        aiIntegrationService.ScoreCallCount.Should().Be(1);
        aiIntegrationService.GenerateReportCallCount.Should().Be(1);
        aiIntegrationService.LastReportRequest.Should().NotBeNull();
        aiIntegrationService.LastReportRequest!.DimensionDetails.Should().ContainKey("technicalAccuracy");
        reportRepository.SavedReport.Should().NotBeNull();
        reportRepository.SavedReport!.ExecutiveSummary.Should().Be("fallback summary");
        interviewRepository.Interview!.Status.Should().Be(InterviewStatuses.Completed);
    }

    [Fact]
    public async Task ProcessInterviewAsync_ShouldReuseExistingScore_WhenScoreAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = BuildInterview(userId, interviewId, InterviewStatuses.GeneratingReport)
        };
        var reportRepository = new InMemoryReportRepository
        {
            SavedScore = new InterviewScore
            {
                InterviewId = interviewId,
                OverallScore = 88,
                RankPercentile = 92,
                ModelVersion = "cached-score",
                DimensionScores = """{"technicalAccuracy":{"score":88,"weight":0.3}}""",
                DimensionDetails = """{"technicalAccuracy":"cached"}""",
                ScoreBreakdown = "{}"
            }
        };
        var aiIntegrationService = new StubAiIntegrationService();
        var aiSettingsService = new StubAiSettingsService
        {
            Provider = new StubAiProvider { ShouldThrow = true }
        };
        var service = new InterviewReportGenerationService(
            interviewRepository,
            reportRepository,
            aiIntegrationService,
            aiSettingsService,
            new StubHubContext(),
            NullLogger<InterviewReportGenerationService>.Instance);

        await service.ProcessInterviewAsync(interviewId);

        aiIntegrationService.ScoreCallCount.Should().Be(0);
        aiIntegrationService.GenerateReportCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessInterviewAsync_ShouldMarkInterviewFailed_WhenReportGenerationThrows()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var interviewRepository = new InMemoryInterviewRepository
        {
            Interview = BuildInterview(userId, interviewId, InterviewStatuses.GeneratingReport)
        };
        var reportRepository = new InMemoryReportRepository();
        var aiIntegrationService = new FailingAiIntegrationService();
        var hubContext = new StubHubContext();
        var service = new InterviewReportGenerationService(
            interviewRepository,
            reportRepository,
            aiIntegrationService,
            new StubAiSettingsService(),
            hubContext,
            NullLogger<InterviewReportGenerationService>.Instance);

        await service.ProcessInterviewAsync(interviewId);

        interviewRepository.Interview!.Status.Should().Be(InterviewStatuses.ReportFailed);
        hubContext.HubClients.SharedClient.ErrorPayloads.Should().ContainSingle();
    }

    private static Interview BuildInterview(Guid userId, Guid interviewId, string status)
    {
        return new Interview
        {
            Id = interviewId,
            UserId = userId,
            PositionCode = "java-backend",
            InterviewMode = "standard",
            Status = status,
            TotalRounds = 3,
            CurrentRound = 3,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-18),
            EndedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            DurationSeconds = 1020,
            Rounds =
            [
                new InterviewRound
                {
                    InterviewId = interviewId,
                    RoundNumber = 1,
                    QuestionId = Guid.NewGuid(),
                    QuestionTitle = "Intro project",
                    QuestionType = "project",
                    QuestionContent = "Describe your order system",
                    UserAnswer = "I owned ordering and inventory consistency",
                    AiFollowUps = ["Show load test details", "Explain retry strategy"]
                },
                new InterviewRound
                {
                    InterviewId = interviewId,
                    RoundNumber = 2,
                    QuestionId = Guid.NewGuid(),
                    QuestionTitle = "Spring transaction",
                    QuestionType = "knowledge",
                    QuestionContent = "How do you pick propagation mode",
                    UserAnswer = "I pick it based on boundary and rollback needs",
                    AiFollowUps = []
                }
            ]
        };
    }
}

file sealed class FailingAiIntegrationService : IAiIntegrationService
{
    public Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ScoreAiResponse
        {
            OverallScore = 75,
            RankPercentile = 80,
            ModelVersion = "score-model",
            DimensionScores = new Dictionary<string, DimensionScoreDto>
            {
                ["technicalAccuracy"] = new() { Score = 75, Weight = 0.3m }
            },
            DimensionDetails = new Dictionary<string, string>
            {
                ["technicalAccuracy"] = "good enough"
            },
            ScoreBreakdown = new Dictionary<string, object>()
        });
    }

    public Task<ReportAiResponse> GenerateReportAsync(ReportAiRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("fallback failed");
    }

    public Task<TrainingPlanAiResponse> GenerateTrainingPlanAsync(TrainingPlanAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ResourceRecommendationAiResponse> RecommendResourcesAsync(ResourceRecommendationAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ProcessDocumentAiResponse> ProcessDocumentAsync(ProcessDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<EnqueueDocumentAiResponse> EnqueueDocumentAsync(EnqueueDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

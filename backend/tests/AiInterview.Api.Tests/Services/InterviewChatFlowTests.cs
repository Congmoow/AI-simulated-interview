using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Interviews;
using AiInterview.Api.Hubs;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services;
using AiInterview.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Api.Tests.Services;

public class InterviewChatFlowTests
{
    [Fact]
    public async Task CreateInterviewAsync_ShouldPersistOpeningAssistantMessage_AndCreateFirstRound()
    {
        var userId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var repository = new ChatFlowInMemoryInterviewRepository();
        var catalog = new ChatFlowCatalogRepository
        {
            Position = new Position
            {
                Code = "java-backend",
                Name = "Java Backend"
            },
            Questions =
            [
                new QuestionBank
                {
                    Id = questionId,
                    PositionCode = "java-backend",
                    Type = "project",
                    Difficulty = "medium",
                    Title = "订单系统项目",
                    Content = "请介绍你做过的订单系统项目"
                }
            ]
        };
        var ai = new ChatFlowAiIntegrationService
        {
            StartResponse = new StartInterviewAiResponse
            {
                Action = "question",
                MessageType = "opening",
                Content = "先请你介绍一个最相关的后端项目。",
                SelectedQuestionId = questionId,
                Suggestions = ["先讲背景"],
                Metadata = new Dictionary<string, object>
                {
                    ["selectedQuestionTitle"] = "订单系统项目"
                }
            }
        };
        var service = CreateService(repository, catalog, ai);

        var result = await service.CreateInterviewAsync(userId, new CreateInterviewRequest
        {
            PositionCode = "java-backend",
            InterviewMode = "standard",
            RoundCount = 5
        });

        result.InterviewId.Should().NotBeEmpty();
        result.Messages.Should().ContainSingle();
        var createdMessages = result.Messages.ToList();
        createdMessages[0].Role.Should().Be("assistant");
        createdMessages[0].MessageType.Should().Be("opening");
        createdMessages[0].Content.Should().Be(catalog.Questions[0].Content);
        repository.Interview!.Messages.Should().ContainSingle();
        repository.Interview.Rounds.Should().ContainSingle();
        repository.Interview.CurrentRound.Should().Be(1);
    }

    [Fact]
    public async Task SubmitAnswerAsync_ShouldAppendFollowUpMessages_WithoutCreatingNewRound()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var repository = ChatFlowInMemoryInterviewRepository.CreateExisting(
            userId,
            interviewId,
            questionId,
            "订单系统项目",
            "请介绍你做过的订单系统项目",
            "opening");
        var catalog = new ChatFlowCatalogRepository
        {
            Position = new Position
            {
                Code = "java-backend",
                Name = "Java Backend"
            },
            Questions =
            [
                new QuestionBank
                {
                    Id = questionId,
                    PositionCode = "java-backend",
                    Type = "project",
                    Difficulty = "medium",
                    Title = "订单系统项目",
                    Content = "请介绍你做过的订单系统项目"
                }
            ]
        };
        var ai = new ChatFlowAiIntegrationService
        {
            AnswerResponse = new AnswerAiResponse
            {
                Action = "follow_up",
                MessageType = "follow_up",
                Content = "你刚才提到库存一致性，具体是怎么做补偿和对账的？",
                Suggestions = ["结合一次事故说明"]
            }
        };
        var service = CreateService(repository, catalog, ai);

        var response = await service.SubmitAnswerAsync(userId, interviewId, new SubmitAnswerRequest
        {
            Answer = "我主要负责订单和库存一致性。"
        });

        response.AiResponse.Type.Should().Be("follow_up");
        repository.Interview!.Messages.Should().HaveCount(3);
        repository.Interview.Rounds.Should().ContainSingle();
        repository.Interview.Messages.Last().Content.Should().Be("你刚才提到库存一致性，具体是怎么做补偿和对账的？");
        repository.Interview.Rounds.Single().AiFollowUps.Should().ContainSingle();
    }

    [Fact]
    public async Task SubmitAnswerAsync_ShouldAppendNextQuestionMessage_AndCreateNextRound()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var firstQuestionId = Guid.NewGuid();
        var nextQuestionId = Guid.NewGuid();
        var repository = ChatFlowInMemoryInterviewRepository.CreateExisting(
            userId,
            interviewId,
            firstQuestionId,
            "订单系统项目",
            "请介绍你做过的订单系统项目",
            "opening");
        var catalog = new ChatFlowCatalogRepository
        {
            Position = new Position
            {
                Code = "java-backend",
                Name = "Java Backend"
            },
            Questions =
            [
                new QuestionBank
                {
                    Id = firstQuestionId,
                    PositionCode = "java-backend",
                    Type = "project",
                    Difficulty = "medium",
                    Title = "订单系统项目",
                    Content = "请介绍你做过的订单系统项目"
                },
                new QuestionBank
                {
                    Id = nextQuestionId,
                    PositionCode = "java-backend",
                    Type = "technical",
                    Difficulty = "medium",
                    Title = "事务传播行为",
                    Content = "说说你如何选择 Spring 事务传播行为"
                }
            ]
        };
        var ai = new ChatFlowAiIntegrationService
        {
            AnswerResponse = new AnswerAiResponse
            {
                Action = "question",
                MessageType = "question",
                Content = "下面切到事务设计。你通常如何选择 Spring 事务传播行为？",
                SelectedQuestionId = nextQuestionId,
                Suggestions = ["结合真实接口讲"]
            }
        };
        var service = CreateService(repository, catalog, ai);

        var response = await service.SubmitAnswerAsync(userId, interviewId, new SubmitAnswerRequest
        {
            Answer = "我先介绍项目背景和职责。"
        });

        response.AiResponse.Type.Should().Be("next_question");
        repository.Interview!.CurrentRound.Should().Be(2);
        repository.Interview.Rounds.Should().HaveCount(2);
        repository.Interview.Messages.Should().HaveCount(3);
        repository.Interview.Messages.Last().RelatedQuestionId.Should().Be(nextQuestionId);
    }

    [Fact]
    public async Task GetInterviewAsync_ShouldReturnCompatMessages_WhenLegacyInterviewHasOnlyRounds()
    {
        var userId = Guid.NewGuid();
        var interviewId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var repository = new ChatFlowInMemoryInterviewRepository
        {
            Interview = new Interview
            {
                Id = interviewId,
                UserId = userId,
                PositionCode = "java-backend",
                InterviewMode = "standard",
                Status = InterviewStatuses.InProgress,
                CurrentRound = 1,
                TotalRounds = 5,
                Position = new Position
                {
                    Code = "java-backend",
                    Name = "Java Backend"
                },
                Rounds =
                [
                    new InterviewRound
                    {
                        InterviewId = interviewId,
                        RoundNumber = 1,
                        QuestionId = questionId,
                        QuestionTitle = "订单系统项目",
                        QuestionType = "project",
                        QuestionContent = "请介绍你做过的订单系统项目",
                        UserAnswer = "我负责订单和库存一致性。",
                        AiFollowUps = ["你如何处理补偿？"],
                        AnsweredAt = DateTimeOffset.UtcNow
                    }
                ]
            }
        };
        var service = CreateService(repository, new ChatFlowCatalogRepository(), new ChatFlowAiIntegrationService());

        var result = await service.GetInterviewAsync(userId, interviewId);

        result.Messages.Should().HaveCount(3);
        result.Messages.Select(item => item.Role).Should().Equal("assistant", "user", "assistant");
        result.Messages.ToList()[0].RelatedQuestionId.Should().Be(questionId);
    }

    private static InterviewService CreateService(
        ChatFlowInMemoryInterviewRepository repository,
        ChatFlowCatalogRepository catalogRepository,
        ChatFlowAiIntegrationService aiIntegrationService)
    {
        return new InterviewService(
            repository,
            catalogRepository,
            new ChatFlowReportRepository(),
            aiIntegrationService,
            new ChatFlowAiSettingsService(),
            new ChatFlowReportQueue(),
            new ChatFlowHubContext(),
            NullLogger<InterviewService>.Instance);
    }
}

sealed class ChatFlowInMemoryInterviewRepository : IInterviewRepository
{
    public Interview? Interview { get; set; }

    public Task AddInterviewAsync(Interview interview, CancellationToken cancellationToken = default)
    {
        Interview = interview;
        return Task.CompletedTask;
    }

    public Task AddRoundAsync(InterviewRound round, CancellationToken cancellationToken = default)
    {
        if (Interview is not null && Interview.Rounds.All(item => item.Id != round.Id))
        {
            Interview.Rounds.Add(round);
        }
        return Task.CompletedTask;
    }

    public Task AddMessageAsync(InterviewMessage message, CancellationToken cancellationToken = default)
    {
        if (Interview is not null && Interview.Messages.All(item => item.Id != message.Id))
        {
            Interview.Messages.Add(message);
        }
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

    public Task<List<InterviewMessage>> GetMessagesAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Interview?.Id == interviewId
                ? Interview.Messages.OrderBy(item => item.Sequence).ToList()
                : new List<InterviewMessage>());
    }

    public Task<int> GetNextMessageSequenceAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var next = Interview?.Messages.Count > 0
            ? Interview.Messages.Max(item => item.Sequence) + 1
            : 1;
        return Task.FromResult(next);
    }

    public Task<List<Guid>> GetInterviewIdsPendingReportGenerationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<Guid>());
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
        return Task.CompletedTask;
    }

    public static ChatFlowInMemoryInterviewRepository CreateExisting(
        Guid userId,
        Guid interviewId,
        Guid questionId,
        string questionTitle,
        string questionContent,
        string messageType)
    {
        return new ChatFlowInMemoryInterviewRepository
        {
            Interview = new Interview
            {
                Id = interviewId,
                UserId = userId,
                PositionCode = "java-backend",
                InterviewMode = "standard",
                Status = InterviewStatuses.InProgress,
                CurrentRound = 1,
                TotalRounds = 5,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                Position = new Position
                {
                    Code = "java-backend",
                    Name = "Java Backend"
                },
                Messages =
                [
                    new InterviewMessage
                    {
                        InterviewId = interviewId,
                        Role = "assistant",
                        MessageType = messageType,
                        Content = questionContent,
                        RelatedQuestionId = questionId,
                        Sequence = 1
                    }
                ],
                Rounds =
                [
                    new InterviewRound
                    {
                        InterviewId = interviewId,
                        RoundNumber = 1,
                        QuestionId = questionId,
                        QuestionTitle = questionTitle,
                        QuestionType = "project",
                        QuestionContent = questionContent
                    }
                ]
            }
        };
    }
}

sealed class ChatFlowCatalogRepository : ICatalogRepository
{
    public Position? Position { get; set; }

    public List<QuestionBank> Questions { get; set; } = [];

    public Task<List<Position>> GetActivePositionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Position?> GetPositionByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Position?.Code == code ? Position : null);
    }

    public Task<List<QuestionBank>> GetQuestionsAsync(string? positionCode, string? type, string? difficulty, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> CountQuestionsAsync(string? positionCode, string? type, string? difficulty, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Questions.FirstOrDefault(item => item.Id == id));
    }

    public Task<QuestionBank?> GetRandomQuestionAsync(string positionCode, IEnumerable<string> questionTypes, IEnumerable<Guid> excludedQuestionIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<List<QuestionBank>> GetQuestionsByPositionAsync(string positionCode, IEnumerable<string> questionTypes, CancellationToken cancellationToken = default)
    {
        var types = questionTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(
            Questions
                .Where(item => item.PositionCode == positionCode && (types.Count == 0 || types.Contains(item.Type)))
                .ToList());
    }

    public Task<List<LearningResource>> GetLearningResourcesAsync(string? positionCode, IEnumerable<string>? dimensions, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> CountQuestionsByPositionAsync(string positionCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Dictionary<string, int>> GetQuestionTypeCountsAsync(string positionCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class ChatFlowAiIntegrationService : IAiIntegrationService
{
    public StartInterviewAiResponse StartResponse { get; set; } = new()
    {
        Action = "question",
        MessageType = "opening",
        Content = "请开始回答。",
        SelectedQuestionId = Guid.NewGuid()
    };

    public AnswerAiResponse AnswerResponse { get; set; } = new()
    {
        Action = "follow_up",
        MessageType = "follow_up",
        Content = "请继续展开。",
    };

    public Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default)
    {
        if (request.QuestionBank.Count > 0 && StartResponse.Metadata.Count == 0)
        {
            StartResponse.Metadata = new Dictionary<string, object>
            {
                ["questionCount"] = request.QuestionBank.Count
            };
        }

        return Task.FromResult(StartResponse);
    }

    public Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AnswerResponse);
    }

    public Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ReportAiResponse> GenerateReportAsync(ReportAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<TrainingPlanAiResponse> GenerateTrainingPlanAsync(TrainingPlanAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ResourceRecommendationAiResponse> RecommendResourcesAsync(ResourceRecommendationAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ProcessDocumentAiResponse> ProcessDocumentAsync(ProcessDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<EnqueueDocumentAiResponse> EnqueueDocumentAsync(EnqueueDocumentAiRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class ChatFlowReportRepository : IReportRepository
{
    public Task<InterviewReport?> GetReportByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default) => Task.FromResult<InterviewReport?>(null);

    public Task<InterviewScore?> GetScoreByInterviewIdAsync(Guid interviewId, CancellationToken cancellationToken = default) => Task.FromResult<InterviewScore?>(null);

    public Task<Dictionary<Guid, InterviewScore>> GetScoresByInterviewIdsAsync(IEnumerable<Guid> interviewIds, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<Guid, InterviewScore>());

    public Task<List<InterviewReport>> GetUserReportsAsync(Guid userId, string? positionCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RecommendationRecord?> GetLatestTrainingPlanAsync(Guid userId, Guid? interviewId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task AddOrUpdateScoreAsync(InterviewScore score, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddOrUpdateReportAsync(InterviewReport report, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddRecommendationRecordsAsync(IEnumerable<RecommendationRecord> records, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class ChatFlowAiSettingsService : IAiSettingsService
{
    public Task<AiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AiSettingsDto> UpdateSettingsAsync(UpdateAiSettingsRequest request, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AiTestResult> TestConnectionAsync(TestAiConnectionRequest? request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IAiProvider?> BuildProviderAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAiProvider?>(null);

    public Task<AiRuntimeSettingsDto?> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<AiRuntimeSettingsDto?>(null);
}

sealed class ChatFlowReportQueue : IInterviewReportGenerationQueue
{
    public ValueTask<bool> EnqueueAsync(Guid interviewId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public bool IsQueued(Guid interviewId) => false;

    public void MarkCompleted(Guid interviewId)
    {
    }
}

sealed class ChatFlowInterviewClient : IInterviewClient
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

sealed class ChatFlowHubClients : IHubClients<IInterviewClient>
{
    private readonly ChatFlowInterviewClient _client = new();

    public IInterviewClient All => _client;

    public IInterviewClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => _client;

    public IInterviewClient Client(string connectionId) => _client;

    public IInterviewClient Clients(IReadOnlyList<string> connectionIds) => _client;

    public IInterviewClient Group(string groupName) => _client;

    public IInterviewClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _client;

    public IInterviewClient Groups(IReadOnlyList<string> groupNames) => _client;

    public IInterviewClient User(string userId) => _client;

    public IInterviewClient Users(IReadOnlyList<string> userIds) => _client;
}

sealed class ChatFlowGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class ChatFlowHubContext : IHubContext<InterviewHub, IInterviewClient>
{
    public IHubClients<IInterviewClient> Clients { get; } = new ChatFlowHubClients();

    public IGroupManager Groups { get; } = new ChatFlowGroupManager();
}

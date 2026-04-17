using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Common;
using AiInterview.Api.DTOs.Interviews;
using AiInterview.Api.Hubs;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AiInterview.Api.Services;

public class InterviewService(
    IInterviewRepository interviewRepository,
    ICatalogRepository catalogRepository,
    IReportRepository reportRepository,
    IAiIntegrationService aiIntegrationService,
    IAiSettingsService aiSettingsService,
    IInterviewReportGenerationQueue reportGenerationQueue,
    IHubContext<InterviewHub, IInterviewClient> hubContext,
    ILogger<InterviewService> logger) : IInterviewService
{
    private const int DefaultMaxMessages = 30;
    private const int DefaultMaxDurationMinutes = 30;

    public async Task<CreateInterviewResponse> CreateInterviewAsync(Guid userId, CreateInterviewRequest request, CancellationToken cancellationToken = default)
    {
        _ = aiSettingsService;

        var position = await catalogRepository.GetPositionByCodeAsync(request.PositionCode, cancellationToken)
            ?? throw new AppException(ErrorCodes.PositionNotFound, "岗位不存在", StatusCodes.Status404NotFound);

        var interviewId = Guid.NewGuid();
        var selectedQuestionTypes = request.QuestionTypes?.Length > 0 ? request.QuestionTypes : QuestionTypes.All;
        var totalRounds = request.RoundCount is > 0 ? request.RoundCount.Value : 5;
        var questionBank = await catalogRepository.GetQuestionsByPositionAsync(position.Code, selectedQuestionTypes, cancellationToken);
        if (questionBank.Count == 0)
        {
            throw new AppException(ErrorCodes.QuestionNotFound, "当前岗位暂无可用题目", StatusCodes.Status404NotFound);
        }

        var limits = new InterviewAiLimitsDto
        {
            MaxMainQuestions = totalRounds,
            CurrentMainQuestionCount = 0,
            MaxMessages = DefaultMaxMessages,
            CurrentMessageCount = 0,
            MaxDurationMinutes = DefaultMaxDurationMinutes,
            CurrentDurationMinutes = 0
        };

        var aiMessage = await aiIntegrationService.StartInterviewAsync(new StartInterviewAiRequest
        {
            InterviewId = interviewId,
            PositionCode = position.Code,
            PositionName = position.Name,
            InterviewMode = string.IsNullOrWhiteSpace(request.InterviewMode) ? InterviewModes.Standard : request.InterviewMode,
            QuestionTypes = selectedQuestionTypes,
            QuestionBank = questionBank.Select(ToCandidateQuestion).ToList(),
            AskedQuestionIds = [],
            RecentMessages = [],
            HistoryAnswerSummaries = [],
            Limits = limits
        }, cancellationToken);

        if (!string.Equals(aiMessage.Action, AiInterviewActions.Question, StringComparison.Ordinal))
        {
            throw new AppException(ErrorCodes.ServiceUnavailable, "首条面试消息必须是主问题", StatusCodes.Status503ServiceUnavailable);
        }

        var selectedQuestion = ResolveSelectedQuestion(questionBank, aiMessage.SelectedQuestionId, []);
        var interview = new Interview
        {
            Id = interviewId,
            UserId = userId,
            PositionCode = position.Code,
            InterviewMode = string.IsNullOrWhiteSpace(request.InterviewMode) ? InterviewModes.Standard : request.InterviewMode,
            Status = InterviewStatuses.InProgress,
            TotalRounds = totalRounds,
            CurrentRound = 1,
            QuestionTypes = selectedQuestionTypes,
            Config = ApplicationMapper.SerializeObject(new
            {
                maxMainQuestions = totalRounds,
                maxMessages = DefaultMaxMessages,
                maxDurationMinutes = DefaultMaxDurationMinutes
            }),
            StartedAt = DateTimeOffset.UtcNow
        };

        var openingMessage = new InterviewMessage
        {
            InterviewId = interview.Id,
            Role = InterviewMessageRoles.Assistant,
            MessageType = string.IsNullOrWhiteSpace(aiMessage.MessageType) ? InterviewMessageTypes.Opening : aiMessage.MessageType,
            Content = aiMessage.Content,
            RelatedQuestionId = selectedQuestion.Id,
            Sequence = 1,
            Metadata = ApplicationMapper.SerializeObject(aiMessage.Metadata)
        };

        var round = new InterviewRound
        {
            InterviewId = interview.Id,
            RoundNumber = 1,
            QuestionId = selectedQuestion.Id,
            QuestionTitle = selectedQuestion.Title,
            QuestionType = selectedQuestion.Type,
            QuestionContent = aiMessage.Content,
            Context = ApplicationMapper.SerializeObject(aiMessage.Metadata)
        };

        interview.Messages.Add(openingMessage);
        interview.Rounds.Add(round);

        await interviewRepository.AddInterviewAsync(interview, cancellationToken);
        await interviewRepository.AddMessageAsync(openingMessage, cancellationToken);
        await interviewRepository.AddRoundAsync(round, cancellationToken);
        await interviewRepository.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).ReceiveQuestion(new
        {
            messageId = openingMessage.Id,
            questionId = selectedQuestion.Id,
            content = openingMessage.Content,
            type = selectedQuestion.Type,
            roundNumber = round.RoundNumber,
            messageType = openingMessage.MessageType
        });

        return new CreateInterviewResponse
        {
            InterviewId = interview.Id,
            PositionCode = position.Code,
            PositionName = position.Name,
            InterviewMode = interview.InterviewMode,
            Status = interview.Status,
            CurrentRound = interview.CurrentRound,
            TotalRounds = interview.TotalRounds,
            CreatedAt = interview.CreatedAt,
            FirstQuestion = new QuestionBriefDto
            {
                QuestionId = selectedQuestion.Id,
                Title = openingMessage.Content,
                Type = selectedQuestion.Type,
                RoundNumber = 1
            },
            Messages = [ToMessageDto(openingMessage)]
        };
    }

    public async Task<InterviewCurrentDetailDto> GetInterviewAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(userId, interviewId, true, cancellationToken);
        var messages = BuildDisplayMessages(interview);

        return new InterviewCurrentDetailDto
        {
            InterviewId = interview.Id,
            PositionCode = interview.PositionCode,
            PositionName = interview.Position?.Name ?? interview.PositionCode,
            InterviewMode = interview.InterviewMode,
            Status = interview.Status,
            CurrentRound = interview.CurrentRound,
            TotalRounds = interview.TotalRounds,
            CreatedAt = interview.CreatedAt,
            Messages = messages.Select(ToMessageDto).ToArray(),
            Rounds = interview.Rounds
                .OrderBy(x => x.RoundNumber)
                .Select(x => new InterviewRoundCurrentDto
                {
                    RoundNumber = x.RoundNumber,
                    Question = new RoundQuestionSummaryDto
                    {
                        QuestionId = x.QuestionId ?? Guid.Empty,
                        Title = x.QuestionTitle,
                        Type = x.QuestionType
                    },
                    UserAnswer = x.UserAnswer,
                    AiFollowUp = x.AiFollowUps.LastOrDefault(),
                    AnsweredAt = x.AnsweredAt
                })
                .ToArray()
        };
    }

    public async Task<SubmitAnswerResponse> SubmitAnswerAsync(Guid userId, Guid interviewId, SubmitAnswerRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            throw new AppException(ErrorCodes.AnswerEmpty, "回答内容不能为空");
        }

        var interview = await GetOwnedInterviewAsync(userId, interviewId, true, cancellationToken);
        if (!string.Equals(interview.Status, InterviewStatuses.InProgress, StringComparison.Ordinal))
        {
            throw new AppException(ErrorCodes.ServiceUnavailable, "当前面试不允许继续作答", StatusCodes.Status409Conflict);
        }

        var currentRound = interview.Rounds.OrderByDescending(x => x.RoundNumber).FirstOrDefault()
            ?? throw new AppException(ErrorCodes.InterviewNotFound, "当前面试缺少主问题记录", StatusCodes.Status404NotFound);

        var now = DateTimeOffset.UtcNow;
        currentRound.UserAnswer = request.Answer.Trim();
        currentRound.UserInputMode = request.InputMode;
        currentRound.VoiceTranscription = request.Transcription;
        currentRound.AnsweredAt = now;
        interview.UpdatedAt = now;

        var userMessage = new InterviewMessage
        {
            InterviewId = interview.Id,
            Role = InterviewMessageRoles.User,
            MessageType = InterviewMessageTypes.Answer,
            Content = request.Answer.Trim(),
            RelatedQuestionId = currentRound.QuestionId,
            Sequence = await interviewRepository.GetNextMessageSequenceAsync(interview.Id, cancellationToken)
        };

        interview.Messages.Add(userMessage);
        await interviewRepository.AddMessageAsync(userMessage, cancellationToken);
        await interviewRepository.SaveChangesAsync(cancellationToken);

        var limits = BuildLimits(interview, BuildDisplayMessages(interview).Count);
        if (limits.CurrentMessageCount >= limits.MaxMessages || limits.CurrentDurationMinutes >= limits.MaxDurationMinutes)
        {
            return await CompleteInterviewFromAiAsync(
                interview,
                currentRound,
                new AnswerAiResponse
                {
                    Action = AiInterviewActions.Finish,
                    MessageType = InterviewMessageTypes.Closing,
                    Content = "本次面试先到这里，接下来为你生成报告。",
                    Suggestions = []
                },
                cancellationToken);
        }

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = true });

        var questionBank = await catalogRepository.GetQuestionsByPositionAsync(interview.PositionCode, interview.QuestionTypes, cancellationToken);
        var aiResponse = await aiIntegrationService.AnswerAsync(new AnswerAiRequest
        {
            InterviewId = interview.Id,
            PositionCode = interview.PositionCode,
            PositionName = interview.Position?.Name ?? interview.PositionCode,
            InterviewMode = interview.InterviewMode,
            QuestionBank = questionBank.Select(ToCandidateQuestion).ToList(),
            AskedQuestionIds = interview.Rounds.Where(x => x.QuestionId.HasValue).Select(x => x.QuestionId!.Value).Distinct().ToList(),
            CurrentMainQuestion = new CurrentMainQuestionAiDto
            {
                RoundNumber = currentRound.RoundNumber,
                QuestionId = currentRound.QuestionId ?? Guid.Empty,
                Title = currentRound.QuestionTitle,
                Type = currentRound.QuestionType,
                AskedContent = currentRound.QuestionContent,
                FollowUpCount = currentRound.FollowUpCount
            },
            RecentMessages = BuildDisplayMessages(interview)
                .OrderBy(x => x.Sequence)
                .TakeLast(12)
                .Select(ToAiMessage)
                .ToList(),
            HistoryAnswerSummaries = BuildHistoryAnswerSummaries(interview),
            Limits = limits
        }, cancellationToken);

        if (string.Equals(aiResponse.Action, AiInterviewActions.FollowUp, StringComparison.Ordinal))
        {
            return await AppendFollowUpAsync(interview, currentRound, aiResponse, cancellationToken);
        }

        if (string.Equals(aiResponse.Action, AiInterviewActions.Finish, StringComparison.Ordinal))
        {
            return await CompleteInterviewFromAiAsync(interview, currentRound, aiResponse, cancellationToken);
        }

        return await AppendNextQuestionAsync(interview, currentRound, questionBank, aiResponse, cancellationToken);
    }

    public async Task<FinishInterviewResponse> FinishInterviewAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("finish_request_received interviewId={InterviewId}", interviewId);

        var interview = await GetOwnedInterviewAsync(userId, interviewId, true, cancellationToken);
        if (interview.Report is not null)
        {
            return new FinishInterviewResponse
            {
                InterviewId = interview.Id,
                Status = InterviewStatuses.Completed,
                ReportId = interview.Report.Id,
                EstimatedTime = 0
            };
        }

        if (interview.Status == InterviewStatuses.GeneratingReport)
        {
            if (!reportGenerationQueue.IsQueued(interview.Id))
            {
                await reportGenerationQueue.EnqueueAsync(interview.Id, cancellationToken);
                logger.LogInformation("report_job_enqueued interviewId={InterviewId} repaired=true", interview.Id);
            }

            return CreateGeneratingReportResponse(interview.Id);
        }

        await BeginReportGenerationAsync(interview, cancellationToken);
        logger.LogInformation("report_job_enqueued interviewId={InterviewId} repaired=false", interview.Id);
        return CreateGeneratingReportResponse(interview.Id);
    }

    public async Task<PagedResult<InterviewHistoryItemDto>> GetHistoryAsync(Guid userId, string? positionCode, string? status, DateOnly? startDate, DateOnly? endDate, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var items = await interviewRepository.GetUserHistoryAsync(userId, positionCode, status, startDate, endDate, safePage, safePageSize, cancellationToken);
        var total = await interviewRepository.CountUserHistoryAsync(userId, positionCode, status, startDate, endDate, cancellationToken);

        return new PagedResult<InterviewHistoryItemDto>
        {
            Items = items.Select(ApplicationMapper.ToInterviewHistoryItemDto).ToArray(),
            Total = total,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public async Task<InterviewDetailDto> GetInterviewDetailAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(userId, interviewId, true, cancellationToken);
        var score = await reportRepository.GetScoreByInterviewIdAsync(interview.Id, cancellationToken);
        var scoreBreakdown = ApplicationMapper.DeserializeObject<Dictionary<string, object>>(score?.ScoreBreakdown, []);

        return new InterviewDetailDto
        {
            InterviewId = interview.Id,
            PositionName = interview.Position?.Name ?? interview.PositionCode,
            InterviewMode = interview.InterviewMode,
            Status = interview.Status,
            TotalScore = score?.OverallScore,
            CreatedAt = interview.CreatedAt,
            CompletedAt = interview.EndedAt,
            Rounds = interview.Rounds
                .OrderBy(x => x.RoundNumber)
                .Select(x => new InterviewRoundDetailDto
                {
                    RoundNumber = x.RoundNumber,
                    Question = new InterviewQuestionDetailDto
                    {
                        QuestionId = x.QuestionId ?? Guid.Empty,
                        Title = x.QuestionTitle,
                        Type = x.QuestionType,
                        Difficulty = scoreBreakdown.TryGetValue($"round{x.RoundNumber}Difficulty", out var difficulty)
                            ? difficulty?.ToString() ?? "medium"
                            : "medium"
                    },
                    UserAnswer = x.UserAnswer,
                    AiFollowUps = x.AiFollowUps,
                    Scores = scoreBreakdown.TryGetValue($"round{x.RoundNumber}", out var roundScore) ? roundScore : null,
                    AnsweredAt = x.AnsweredAt
                })
                .ToArray()
        };
    }

    private async Task<SubmitAnswerResponse> AppendFollowUpAsync(
        Interview interview,
        InterviewRound currentRound,
        AnswerAiResponse aiResponse,
        CancellationToken cancellationToken)
    {
        var followUpMessage = new InterviewMessage
        {
            InterviewId = interview.Id,
            Role = InterviewMessageRoles.Assistant,
            MessageType = string.IsNullOrWhiteSpace(aiResponse.MessageType) ? InterviewMessageTypes.FollowUp : aiResponse.MessageType,
            Content = aiResponse.Content,
            RelatedQuestionId = currentRound.QuestionId,
            Sequence = await interviewRepository.GetNextMessageSequenceAsync(interview.Id, cancellationToken),
            Metadata = ApplicationMapper.SerializeObject(aiResponse.Metadata)
        };

        currentRound.AiFollowUps = currentRound.AiFollowUps.Append(aiResponse.Content).ToArray();
        currentRound.FollowUpCount += 1;
        currentRound.Context = ApplicationMapper.SerializeObject(aiResponse.Metadata);
        interview.Messages.Add(followUpMessage);

        await interviewRepository.AddMessageAsync(followUpMessage, cancellationToken);
        await interviewRepository.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = false });
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).ReceiveFollowUp(new
        {
            messageId = followUpMessage.Id,
            questionId = currentRound.QuestionId,
            content = followUpMessage.Content,
            messageType = followUpMessage.MessageType,
            suggestions = aiResponse.Suggestions
        });

        return new SubmitAnswerResponse
        {
            RoundNumber = currentRound.RoundNumber,
            InterviewStatus = interview.Status,
            NextRoundAvailable = false,
            AiResponse = new AiResponseDto
            {
                Type = AiInterviewActions.FollowUp,
                Content = aiResponse.Content,
                Suggestions = aiResponse.Suggestions
            }
        };
    }

    private async Task<SubmitAnswerResponse> AppendNextQuestionAsync(
        Interview interview,
        InterviewRound currentRound,
        List<QuestionBank> questionBank,
        AnswerAiResponse aiResponse,
        CancellationToken cancellationToken)
    {
        if (interview.CurrentRound >= interview.TotalRounds)
        {
            return await CompleteInterviewFromAiAsync(
                interview,
                currentRound,
                new AnswerAiResponse
                {
                    Action = AiInterviewActions.Finish,
                    MessageType = InterviewMessageTypes.Closing,
                    Content = "主问题轮次已达到上限，接下来为你生成报告。",
                    Suggestions = []
                },
                cancellationToken);
        }

        var askedQuestionIds = interview.Rounds
            .Where(x => x.QuestionId.HasValue)
            .Select(x => x.QuestionId!.Value)
            .Distinct()
            .ToList();
        var selectedQuestion = ResolveSelectedQuestion(questionBank, aiResponse.SelectedQuestionId, askedQuestionIds);

        var nextRoundNumber = currentRound.RoundNumber + 1;
        interview.CurrentRound = nextRoundNumber;

        var questionMessage = new InterviewMessage
        {
            InterviewId = interview.Id,
            Role = InterviewMessageRoles.Assistant,
            MessageType = string.IsNullOrWhiteSpace(aiResponse.MessageType) ? InterviewMessageTypes.Question : aiResponse.MessageType,
            Content = aiResponse.Content,
            RelatedQuestionId = selectedQuestion.Id,
            Sequence = await interviewRepository.GetNextMessageSequenceAsync(interview.Id, cancellationToken),
            Metadata = ApplicationMapper.SerializeObject(aiResponse.Metadata)
        };

        var nextRound = new InterviewRound
        {
            InterviewId = interview.Id,
            RoundNumber = nextRoundNumber,
            QuestionId = selectedQuestion.Id,
            QuestionTitle = selectedQuestion.Title,
            QuestionType = selectedQuestion.Type,
            QuestionContent = aiResponse.Content,
            Context = ApplicationMapper.SerializeObject(aiResponse.Metadata)
        };

        interview.Messages.Add(questionMessage);
        interview.Rounds.Add(nextRound);

        await interviewRepository.AddMessageAsync(questionMessage, cancellationToken);
        await interviewRepository.AddRoundAsync(nextRound, cancellationToken);
        await interviewRepository.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = false });
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).ReceiveQuestion(new
        {
            messageId = questionMessage.Id,
            questionId = selectedQuestion.Id,
            content = questionMessage.Content,
            type = selectedQuestion.Type,
            roundNumber = nextRound.RoundNumber,
            messageType = questionMessage.MessageType
        });
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).InterviewStatusChanged(new
        {
            status = interview.Status,
            currentRound = interview.CurrentRound
        });

        return new SubmitAnswerResponse
        {
            RoundNumber = currentRound.RoundNumber,
            InterviewStatus = interview.Status,
            NextRoundAvailable = true,
            AiResponse = new AiResponseDto
            {
                Type = "next_question",
                Content = aiResponse.Content,
                Suggestions = aiResponse.Suggestions
            }
        };
    }

    private async Task<SubmitAnswerResponse> CompleteInterviewFromAiAsync(
        Interview interview,
        InterviewRound currentRound,
        AnswerAiResponse aiResponse,
        CancellationToken cancellationToken)
    {
        var closingMessage = new InterviewMessage
        {
            InterviewId = interview.Id,
            Role = InterviewMessageRoles.Assistant,
            MessageType = string.IsNullOrWhiteSpace(aiResponse.MessageType) ? InterviewMessageTypes.Closing : aiResponse.MessageType,
            Content = aiResponse.Content,
            RelatedQuestionId = currentRound.QuestionId,
            Sequence = await interviewRepository.GetNextMessageSequenceAsync(interview.Id, cancellationToken),
            Metadata = ApplicationMapper.SerializeObject(aiResponse.Metadata)
        };

        interview.Messages.Add(closingMessage);
        await interviewRepository.AddMessageAsync(closingMessage, cancellationToken);
        await interviewRepository.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = false });
        await BeginReportGenerationAsync(interview, cancellationToken);

        return new SubmitAnswerResponse
        {
            RoundNumber = currentRound.RoundNumber,
            InterviewStatus = interview.Status,
            NextRoundAvailable = false,
            AiResponse = new AiResponseDto
            {
                Type = AiInterviewActions.Finish,
                Content = aiResponse.Content,
                Suggestions = aiResponse.Suggestions
            }
        };
    }

    private async Task BeginReportGenerationAsync(Interview interview, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (interview.EndedAt is null)
        {
            interview.EndedAt = now;
            interview.DurationSeconds = interview.StartedAt.HasValue
                ? (int)(interview.EndedAt.Value - interview.StartedAt.Value).TotalSeconds
                : 0;
        }

        interview.Status = InterviewStatuses.GeneratingReport;
        interview.UpdatedAt = now;

        await interviewRepository.SaveChangesAsync(cancellationToken);
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).InterviewStatusChanged(new
        {
            status = interview.Status,
            currentRound = interview.CurrentRound
        });
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).ReportProgress(new
        {
            progress = 10,
            stage = "ended",
            estimatedTime = 30
        });

        await reportGenerationQueue.EnqueueAsync(interview.Id, cancellationToken);
    }

    private async Task<Interview> GetOwnedInterviewAsync(Guid userId, Guid interviewId, bool includeDetails, CancellationToken cancellationToken)
    {
        var interview = includeDetails
            ? await interviewRepository.GetByIdWithDetailsAsync(interviewId, cancellationToken)
            : await interviewRepository.GetByIdAsync(interviewId, cancellationToken);

        if (interview is null || interview.UserId != userId)
        {
            throw new AppException(ErrorCodes.InterviewNotFound, "面试不存在", StatusCodes.Status404NotFound);
        }

        return interview;
    }

    private static CandidateQuestionDto ToCandidateQuestion(QuestionBank question)
    {
        return new CandidateQuestionDto
        {
            QuestionId = question.Id,
            Title = question.Title,
            Type = question.Type,
            Content = question.Content,
            Difficulty = question.Difficulty
        };
    }

    private static InterviewMessageAiDto ToAiMessage(InterviewMessage message)
    {
        return new InterviewMessageAiDto
        {
            Role = message.Role,
            MessageType = message.MessageType,
            Content = message.Content,
            RelatedQuestionId = message.RelatedQuestionId,
            Sequence = message.Sequence
        };
    }

    private static InterviewMessageDto ToMessageDto(InterviewMessage message)
    {
        return new InterviewMessageDto
        {
            Id = message.Id,
            Role = message.Role,
            MessageType = message.MessageType,
            Content = message.Content,
            RelatedQuestionId = message.RelatedQuestionId,
            Sequence = message.Sequence,
            Metadata = ApplicationMapper.DeserializeObject<Dictionary<string, object>>(message.Metadata, []),
            CreatedAt = message.CreatedAt
        };
    }

    private static QuestionBank ResolveSelectedQuestion(IEnumerable<QuestionBank> questionBank, Guid? selectedQuestionId, IEnumerable<Guid> askedQuestionIds)
    {
        var asked = askedQuestionIds.ToHashSet();
        if (selectedQuestionId.HasValue)
        {
            var matched = questionBank.FirstOrDefault(item => item.Id == selectedQuestionId.Value && !asked.Contains(item.Id));
            if (matched is not null)
            {
                return matched;
            }
        }

        var fallback = questionBank.FirstOrDefault(item => !asked.Contains(item.Id)) ?? questionBank.First();
        return fallback;
    }

    private static List<InterviewMessage> BuildDisplayMessages(Interview interview)
    {
        if (interview.Messages.Count > 0)
        {
            return interview.Messages.OrderBy(item => item.Sequence).ToList();
        }

        var messages = new List<InterviewMessage>();
        var sequence = 1;
        foreach (var round in interview.Rounds.OrderBy(item => item.RoundNumber))
        {
            messages.Add(new InterviewMessage
            {
                InterviewId = interview.Id,
                Role = InterviewMessageRoles.Assistant,
                MessageType = round.RoundNumber == 1 ? InterviewMessageTypes.Opening : InterviewMessageTypes.Question,
                Content = round.QuestionContent,
                RelatedQuestionId = round.QuestionId,
                Sequence = sequence++,
                CreatedAt = round.CreatedAt
            });

            if (!string.IsNullOrWhiteSpace(round.UserAnswer))
            {
                messages.Add(new InterviewMessage
                {
                    InterviewId = interview.Id,
                    Role = InterviewMessageRoles.User,
                    MessageType = InterviewMessageTypes.Answer,
                    Content = round.UserAnswer,
                    RelatedQuestionId = round.QuestionId,
                    Sequence = sequence++,
                    CreatedAt = round.AnsweredAt ?? round.CreatedAt
                });
            }

            foreach (var followUp in round.AiFollowUps)
            {
                messages.Add(new InterviewMessage
                {
                    InterviewId = interview.Id,
                    Role = InterviewMessageRoles.Assistant,
                    MessageType = InterviewMessageTypes.FollowUp,
                    Content = followUp,
                    RelatedQuestionId = round.QuestionId,
                    Sequence = sequence++,
                    CreatedAt = round.AnsweredAt ?? round.CreatedAt
                });
            }
        }

        return messages;
    }

    private static List<string> BuildHistoryAnswerSummaries(Interview interview)
    {
        return interview.Rounds
            .OrderBy(item => item.RoundNumber)
            .Where(item => !string.IsNullOrWhiteSpace(item.UserAnswer))
            .Select(item => $"第{item.RoundNumber}题：{item.QuestionTitle}；回答：{item.UserAnswer}")
            .TakeLast(5)
            .ToList();
    }

    private static InterviewAiLimitsDto BuildLimits(Interview interview, int messageCount)
    {
        var config = ApplicationMapper.DeserializeObject<Dictionary<string, int>>(interview.Config, []);
        var durationMinutes = interview.StartedAt.HasValue
            ? (int)Math.Max(0, Math.Floor((DateTimeOffset.UtcNow - interview.StartedAt.Value).TotalMinutes))
            : 0;

        return new InterviewAiLimitsDto
        {
            MaxMainQuestions = config.GetValueOrDefault("maxMainQuestions", interview.TotalRounds),
            CurrentMainQuestionCount = interview.CurrentRound,
            MaxMessages = config.GetValueOrDefault("maxMessages", DefaultMaxMessages),
            CurrentMessageCount = messageCount,
            MaxDurationMinutes = config.GetValueOrDefault("maxDurationMinutes", DefaultMaxDurationMinutes),
            CurrentDurationMinutes = durationMinutes
        };
    }

    private static FinishInterviewResponse CreateGeneratingReportResponse(Guid interviewId)
    {
        return new FinishInterviewResponse
        {
            InterviewId = interviewId,
            Status = InterviewStatuses.GeneratingReport,
            ReportId = null,
            EstimatedTime = 30
        };
    }
}

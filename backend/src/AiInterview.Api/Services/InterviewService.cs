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
    public async Task<CreateInterviewResponse> CreateInterviewAsync(Guid userId, CreateInterviewRequest request, CancellationToken cancellationToken = default)
    {
        _ = aiSettingsService;

        var position = await catalogRepository.GetPositionByCodeAsync(request.PositionCode, cancellationToken)
            ?? throw new AppException(ErrorCodes.PositionNotFound, "岗位不存在", StatusCodes.Status404NotFound);

        var selectedQuestionTypes = request.QuestionTypes?.Length > 0 ? request.QuestionTypes : QuestionTypes.All;
        var firstQuestion = await catalogRepository.GetRandomQuestionAsync(position.Code, selectedQuestionTypes, [], cancellationToken)
            ?? throw new AppException(ErrorCodes.QuestionNotFound, "当前岗位暂无可用题目", StatusCodes.Status404NotFound);

        var aiQuestion = await aiIntegrationService.StartInterviewAsync(new StartInterviewAiRequest
        {
            InterviewId = Guid.NewGuid(),
            PositionCode = position.Code,
            InterviewMode = request.InterviewMode,
            RoundNumber = 1,
            QuestionTypes = selectedQuestionTypes,
            SourceQuestion = ToCandidateQuestion(firstQuestion)
        }, cancellationToken);

        var interview = new Interview
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PositionCode = position.Code,
            InterviewMode = string.IsNullOrWhiteSpace(request.InterviewMode) ? InterviewModes.Standard : request.InterviewMode,
            Status = InterviewStatuses.InProgress,
            TotalRounds = request.RoundCount is > 0 ? request.RoundCount.Value : 5,
            CurrentRound = 1,
            QuestionTypes = selectedQuestionTypes,
            StartedAt = DateTimeOffset.UtcNow
        };

        var round = new InterviewRound
        {
            InterviewId = interview.Id,
            RoundNumber = 1,
            QuestionId = firstQuestion.Id,
            QuestionTitle = aiQuestion.Title,
            QuestionType = aiQuestion.Type,
            QuestionContent = aiQuestion.Content
        };

        await interviewRepository.AddInterviewAsync(interview, cancellationToken);
        await interviewRepository.AddRoundAsync(round, cancellationToken);
        await interviewRepository.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).ReceiveQuestion(new
        {
            questionId = round.QuestionId,
            title = round.QuestionTitle,
            type = round.QuestionType,
            roundNumber = round.RoundNumber
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
                QuestionId = round.QuestionId ?? Guid.Empty,
                Title = round.QuestionTitle,
                Type = round.QuestionType,
                RoundNumber = round.RoundNumber
            }
        };
    }

    public async Task<InterviewCurrentDetailDto> GetInterviewAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(userId, interviewId, true, cancellationToken);

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
        var currentRound = interview.Rounds.OrderByDescending(x => x.RoundNumber).FirstOrDefault()
            ?? throw new AppException(ErrorCodes.InterviewNotFound, "面试轮次不存在", StatusCodes.Status404NotFound);

        currentRound.UserAnswer = request.Answer.Trim();
        currentRound.UserInputMode = request.InputMode;
        currentRound.VoiceTranscription = request.Transcription;
        currentRound.AnsweredAt = DateTimeOffset.UtcNow;
        interview.UpdatedAt = DateTimeOffset.UtcNow;

        var nextQuestionCandidate = interview.CurrentRound < interview.TotalRounds
            ? await catalogRepository.GetRandomQuestionAsync(
                interview.PositionCode,
                interview.QuestionTypes,
                interview.Rounds.Where(x => x.QuestionId.HasValue).Select(x => x.QuestionId!.Value),
                cancellationToken)
            : null;

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = true });

        var aiResponse = await aiIntegrationService.AnswerAsync(new AnswerAiRequest
        {
            InterviewId = interview.Id,
            RoundNumber = currentRound.RoundNumber,
            InterviewMode = interview.InterviewMode,
            PositionCode = interview.PositionCode,
            QuestionTitle = currentRound.QuestionTitle,
            QuestionContent = currentRound.QuestionContent,
            Answer = request.Answer.Trim(),
            FollowUpCount = currentRound.FollowUpCount,
            CurrentRound = interview.CurrentRound,
            TotalRounds = interview.TotalRounds,
            NextQuestionCandidate = nextQuestionCandidate is null ? null : ToCandidateQuestion(nextQuestionCandidate)
        }, cancellationToken);

        if (aiResponse.Type == "follow_up")
        {
            currentRound.AiFollowUps = currentRound.AiFollowUps.Append(aiResponse.Content).ToArray();
            currentRound.FollowUpCount += 1;
            await interviewRepository.SaveChangesAsync(cancellationToken);

            await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = false });
            await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).ReceiveFollowUp(new
            {
                questionId = currentRound.QuestionId,
                content = aiResponse.Content,
                suggestions = aiResponse.Suggestions
            });

            return new SubmitAnswerResponse
            {
                RoundNumber = currentRound.RoundNumber,
                InterviewStatus = interview.Status,
                NextRoundAvailable = false,
                AiResponse = new AiResponseDto
                {
                    Type = aiResponse.Type,
                    Content = aiResponse.Content,
                    Suggestions = aiResponse.Suggestions
                }
            };
        }

        var nextQuestion = aiResponse.NextQuestion ?? (nextQuestionCandidate is null ? null : ToCandidateQuestion(nextQuestionCandidate));
        if (nextQuestion is null)
        {
            await interviewRepository.SaveChangesAsync(cancellationToken);
            await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = false });

            return new SubmitAnswerResponse
            {
                RoundNumber = currentRound.RoundNumber,
                InterviewStatus = interview.Status,
                NextRoundAvailable = false,
                AiResponse = new AiResponseDto
                {
                    Type = "follow_up",
                    Content = "当前问题已经完成，可以继续下一轮或结束面试。",
                    Suggestions = ["继续追问", "切换下一题", "主动结束"]
                }
            };
        }

        var nextRoundNumber = currentRound.RoundNumber + 1;
        interview.CurrentRound = nextRoundNumber;

        var nextRound = new InterviewRound
        {
            InterviewId = interview.Id,
            RoundNumber = nextRoundNumber,
            QuestionId = nextQuestion.QuestionId == Guid.Empty ? nextQuestionCandidate?.Id : nextQuestion.QuestionId,
            QuestionTitle = nextQuestion.Title,
            QuestionType = nextQuestion.Type,
            QuestionContent = nextQuestion.Content
        };

        await interviewRepository.AddRoundAsync(nextRound, cancellationToken);
        await interviewRepository.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).TypingIndicator(new { isTyping = false });
        await hubContext.Clients.Group(InterviewHub.BuildRoomName(interview.Id)).ReceiveQuestion(new
        {
            questionId = nextRound.QuestionId,
            title = nextRound.QuestionTitle,
            type = nextRound.QuestionType,
            roundNumber = nextRound.RoundNumber
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
                Content = nextRound.QuestionTitle,
                Suggestions = aiResponse.Suggestions
            }
        };
    }

    public async Task<FinishInterviewResponse> FinishInterviewAsync(Guid userId, Guid interviewId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("finish_request_received，interviewId={InterviewId}", interviewId);

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
                logger.LogInformation("report_job_enqueued，interviewId={InterviewId} repaired=true", interview.Id);
            }

            return CreateGeneratingReportResponse(interview.Id);
        }

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
        logger.LogInformation("report_job_enqueued，interviewId={InterviewId} repaired=false", interview.Id);

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

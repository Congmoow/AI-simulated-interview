using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.DTOs.Auth;
using AiInterview.Api.DTOs.Interviews;
using AiInterview.Api.DTOs.Positions;
using AiInterview.Api.DTOs.Questions;
using AiInterview.Api.DTOs.Recommendations;
using AiInterview.Api.DTOs.Reports;
using AiInterview.Api.Models.Entities;
using System.Text.Json;

namespace AiInterview.Api.Mappings;

public static class ApplicationMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RegisterResponse ToRegisterResponse(User user) => new()
    {
        UserId = user.Id,
        Username = user.Username,
        Email = user.Email
    };

    public static CurrentUserDto ToCurrentUserDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        Phone = user.Phone,
        Role = user.Role,
        AvatarUrl = user.AvatarUrl,
        CreatedAt = user.CreatedAt,
        TargetPosition = user.TargetPosition is null ? null : new PositionOptionDto
        {
            Code = user.TargetPosition.Code,
            Name = user.TargetPosition.Name
        }
    };

    public static PositionSummaryDto ToPositionSummaryDto(Position position, int questionCount) => new()
    {
        Code = position.Code,
        Name = position.Name,
        Description = position.Description,
        Tags = position.Tags,
        QuestionCount = questionCount
    };

    public static PositionDetailDto ToPositionDetailDto(Position position, int questionCount, Dictionary<string, int> typeCounts) => new()
    {
        Code = position.Code,
        Name = position.Name,
        Description = position.Description,
        Tags = position.Tags,
        QuestionCount = questionCount,
        QuestionTypes = typeCounts.Select(x => new QuestionTypeCountDto
        {
            Type = x.Key,
            Name = QuestionTypes.DisplayNames.GetValueOrDefault(x.Key, x.Key),
            Count = x.Value
        }).ToArray(),
        Difficulty = ["初级", "中级", "高级"]
    };

    public static QuestionSummaryDto ToQuestionSummaryDto(QuestionBank question) => new()
    {
        Id = question.Id,
        PositionCode = question.PositionCode,
        Type = question.Type,
        TypeName = QuestionTypes.DisplayNames.GetValueOrDefault(question.Type, question.Type),
        Difficulty = question.Difficulty,
        Title = question.Title,
        Tags = question.Tags,
        IdealAnswerHint = string.IsNullOrWhiteSpace(question.IdealAnswer) ? null : Truncate(question.IdealAnswer, 60),
        CreatedAt = question.CreatedAt
    };

    public static QuestionDetailDto ToQuestionDetailDto(QuestionBank question, string positionName) => new()
    {
        Id = question.Id,
        PositionCode = question.PositionCode,
        PositionName = positionName,
        Type = question.Type,
        TypeName = QuestionTypes.DisplayNames.GetValueOrDefault(question.Type, question.Type),
        Difficulty = question.Difficulty,
        Title = question.Title,
        Content = question.Content,
        Tags = question.Tags,
        IdealAnswer = question.IdealAnswer,
        ScoringRubric = DeserializeObject<Dictionary<string, decimal>>(question.ScoringRubric, []),
        RelatedKnowledgeIds = question.RelatedKnowledgeIds
    };

    public static InterviewHistoryItemDto ToInterviewHistoryItemDto(Interview interview) => new()
    {
        InterviewId = interview.Id,
        PositionName = interview.Position?.Name ?? interview.PositionCode,
        InterviewMode = interview.InterviewMode,
        Status = interview.Status,
        TotalScore = interview.Score?.OverallScore,
        RoundCount = interview.Rounds.Count,
        Duration = interview.DurationSeconds ?? 0,
        CreatedAt = interview.CreatedAt,
        CompletedAt = interview.EndedAt
    };

    public static InterviewReportDto ToInterviewReportDto(InterviewReport report, InterviewScore? score) => new()
    {
        ReportId = report.Id,
        InterviewId = report.InterviewId,
        PositionName = report.Position?.Name ?? report.PositionCode,
        OverallScore = report.OverallScore,
        DimensionScores = DeserializeObject<Dictionary<string, DimensionScoreDto>>(score?.DimensionScores, []),
        Strengths = report.Strengths,
        Weaknesses = report.Weaknesses,
        LearningSuggestions = report.LearningSuggestions,
        TrainingPlan = DeserializeObject<object[]>(report.TrainingPlan, []),
        GeneratedAt = report.GeneratedAt
    };

    public static ResourceRecommendationDto ToResourceRecommendationDto(LearningResource resource, decimal matchScore) => new()
    {
        ResourceId = resource.Id,
        Title = resource.Title,
        Type = resource.Type,
        Provider = resource.Provider,
        Url = resource.Url,
        CoverUrl = resource.CoverUrl,
        TargetDimensions = resource.TargetDimensions,
        Difficulty = resource.Difficulty,
        Duration = resource.Duration,
        ReadingTime = resource.ReadingTime,
        Rating = resource.Rating,
        MatchScore = matchScore
    };

    public static QuestionAdminDto ToQuestionAdminDto(QuestionBank question) => new()
    {
        Id = question.Id,
        PositionCode = question.PositionCode,
        Type = question.Type,
        Difficulty = question.Difficulty,
        Title = question.Title,
        CreatedAt = question.CreatedAt,
        UpdatedAt = question.UpdatedAt
    };

    public static KnowledgeDocumentListItemDto ToKnowledgeDocumentListItemDto(KnowledgeDocument document) => new()
    {
        DocumentId = document.Id,
        Title = document.Title,
        PositionCode = document.PositionCode,
        Status = document.Status,
        ChunkCount = document.ChunkCount,
        FileSize = FormatFileSize(document.FileSize),
        CreatedAt = document.CreatedAt,
        ProcessedAt = document.ProcessedAt
    };

    public static T DeserializeObject<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static string SerializeObject<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : $"{value[..maxLength]}...";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / 1024d / 1024d:0.#}MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024d:0.#}KB";
        }

        return $"{bytes}B";
    }
}

using AiInterview.Api.DTOs.Reports;

namespace AiInterview.Api.Services.Interfaces;

public interface IAiIntegrationService
{
    Task<StartInterviewAiResponse> StartInterviewAsync(StartInterviewAiRequest request, CancellationToken cancellationToken = default);

    Task<AnswerAiResponse> AnswerAsync(AnswerAiRequest request, CancellationToken cancellationToken = default);

    Task<FinishInterviewAiResponse> FinishInterviewAsync(FinishInterviewAiRequest request, CancellationToken cancellationToken = default);

    Task<ScoreAiResponse> ScoreAsync(ScoreAiRequest request, CancellationToken cancellationToken = default);

    Task<ReportAiResponse> GenerateReportAsync(ReportAiRequest request, CancellationToken cancellationToken = default);

    Task<TrainingPlanAiResponse> GenerateTrainingPlanAsync(TrainingPlanAiRequest request, CancellationToken cancellationToken = default);

    Task<ResourceRecommendationAiResponse> RecommendResourcesAsync(ResourceRecommendationAiRequest request, CancellationToken cancellationToken = default);

    Task<ProcessDocumentAiResponse> ProcessDocumentAsync(ProcessDocumentAiRequest request, CancellationToken cancellationToken = default);

    Task<EnqueueDocumentAiResponse> EnqueueDocumentAsync(EnqueueDocumentAiRequest request, CancellationToken cancellationToken = default);
}

public class StartInterviewAiRequest
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string InterviewMode { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public string[] QuestionTypes { get; set; } = [];
    public CandidateQuestionDto SourceQuestion { get; set; } = new();
}

public class StartInterviewAiResponse
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[] Suggestions { get; set; } = [];
}

public class AnswerAiRequest
{
    public Guid InterviewId { get; set; }
    public int RoundNumber { get; set; }
    public string InterviewMode { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
    public string QuestionTitle { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int FollowUpCount { get; set; }
    public int CurrentRound { get; set; }
    public int TotalRounds { get; set; }
    public CandidateQuestionDto? NextQuestionCandidate { get; set; }
}

public class AnswerAiResponse
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[] Suggestions { get; set; } = [];
    public CandidateQuestionDto? NextQuestion { get; set; }
}

public class CandidateQuestionDto
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
}

public class FinishInterviewAiRequest
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public int TotalRounds { get; set; }
}

public class FinishInterviewAiResponse
{
    public string Summary { get; set; } = string.Empty;
}

public class ScoreAiRequest
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public List<ScoreAiRoundDto> Rounds { get; set; } = [];
}

public class ScoreAiRoundDto
{
    public int RoundNumber { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public string QuestionTitle { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public string[] FollowUps { get; set; } = [];
}

public class ScoreAiResponse
{
    public decimal OverallScore { get; set; }
    public Dictionary<string, DimensionScoreDto> DimensionScores { get; set; } = [];
    public Dictionary<string, string> DimensionDetails { get; set; } = [];
    public Dictionary<string, object> ScoreBreakdown { get; set; } = [];
    public decimal RankPercentile { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
}

public class ReportAiRequest
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public decimal OverallScore { get; set; }
    public Dictionary<string, DimensionScoreDto> DimensionScores { get; set; } = [];
}

public class ReportAiResponse
{
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string[] Strengths { get; set; } = [];
    public string[] Weaknesses { get; set; } = [];
    public Dictionary<string, object> DetailedAnalysis { get; set; } = [];
    public string[] LearningSuggestions { get; set; } = [];
    public object[] TrainingPlan { get; set; } = [];
    public string[] NextInterviewFocus { get; set; } = [];
    public string ModelVersion { get; set; } = string.Empty;
}

public class ResourceRecommendationAiRequest
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string[] Weaknesses { get; set; } = [];
}

public class ResourceRecommendationAiResponse
{
    public string[] TargetDimensions { get; set; } = [];
    public Dictionary<string, decimal> MatchScores { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public class TrainingPlanAiRequest
{
    public Guid InterviewId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string[] Weaknesses { get; set; } = [];
}

public class TrainingPlanAiResponse
{
    public int Weeks { get; set; } = 4;
    public string DailyCommitment { get; set; } = string.Empty;
    public string[] Goals { get; set; } = [];
    public object[] Schedule { get; set; } = [];
    public object[] Milestones { get; set; } = [];
}

public class ProcessDocumentAiRequest
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class ChunkResultDto
{
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class ProcessDocumentAiResponse
{
    public Guid DocumentId { get; set; }
    public List<ChunkResultDto> Chunks { get; set; } = [];
}

public class EnqueueDocumentAiRequest
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class EnqueueDocumentAiResponse
{
    public string TaskId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
}

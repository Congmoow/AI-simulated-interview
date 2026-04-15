using AiInterview.Api.DTOs.Recommendations;
using AiInterview.Api.Mappings;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;

namespace AiInterview.Api.Services;

public class RecommendationService(ICatalogRepository catalogRepository, IReportRepository reportRepository) : IRecommendationService
{
    public async Task<IReadOnlyCollection<ResourceRecommendationDto>> GetResourcesAsync(Guid userId, string? positionCode, string? dimensions, int limit, CancellationToken cancellationToken = default)
    {
        var dimensionList = dimensions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var resources = await catalogRepository.GetLearningResourcesAsync(positionCode, dimensionList, Math.Clamp(limit, 1, 20), cancellationToken);
        return resources.Select(resource =>
        {
            var overlap = dimensionList is { Length: > 0 }
                ? resource.TargetDimensions.Intersect(dimensionList, StringComparer.OrdinalIgnoreCase).Count()
                : 0;
            var matchScore = overlap > 0 ? 80 + overlap * 5 : 72;
            return ApplicationMapper.ToResourceRecommendationDto(resource, Math.Min(matchScore, 99));
        }).ToArray();
    }

    public async Task<TrainingPlanDto> GetTrainingPlanAsync(Guid userId, Guid? interviewId, int weeks, CancellationToken cancellationToken = default)
    {
        var record = await reportRepository.GetLatestTrainingPlanAsync(userId, interviewId, cancellationToken);
        if (record is null || string.IsNullOrWhiteSpace(record.TrainingPlan))
        {
            return new TrainingPlanDto
            {
                PlanId = Guid.NewGuid(),
                Weeks = weeks <= 0 ? 4 : weeks,
                DailyCommitment = "2小时",
                Goals = ["夯实技术基础", "提升结构化表达"],
                Schedule = [],
                Milestones = [],
                GeneratedAt = DateTimeOffset.UtcNow
            };
        }

        var payload = ApplicationMapper.DeserializeObject(record.TrainingPlan, new TrainingPlanPayload());
        return new TrainingPlanDto
        {
            PlanId = record.Id,
            Weeks = payload.Weeks > 0 ? payload.Weeks : 4,
            DailyCommitment = string.IsNullOrWhiteSpace(payload.DailyCommitment) ? "2小时" : payload.DailyCommitment,
            Goals = payload.Goals ?? [],
            Schedule = payload.Schedule ?? [],
            Milestones = payload.Milestones ?? [],
            GeneratedAt = record.CreatedAt
        };
    }

    private sealed class TrainingPlanPayload
    {
        public int Weeks { get; set; }

        public string DailyCommitment { get; set; } = string.Empty;

        public string[] Goals { get; set; } = [];

        public object[] Schedule { get; set; } = [];

        public object[] Milestones { get; set; } = [];
    }
}

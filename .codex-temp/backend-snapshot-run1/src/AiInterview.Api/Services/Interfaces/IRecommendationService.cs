using AiInterview.Api.DTOs.Recommendations;

namespace AiInterview.Api.Services.Interfaces;

public interface IRecommendationService
{
    Task<IReadOnlyCollection<ResourceRecommendationDto>> GetResourcesAsync(Guid userId, string? positionCode, string? dimensions, int limit, CancellationToken cancellationToken = default);

    Task<TrainingPlanDto> GetTrainingPlanAsync(Guid userId, Guid? interviewId, int weeks, CancellationToken cancellationToken = default);
}

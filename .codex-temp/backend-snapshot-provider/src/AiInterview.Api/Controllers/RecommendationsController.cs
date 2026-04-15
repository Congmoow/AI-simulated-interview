using AiInterview.Api.Extensions;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Authorize]
[Route("api/v1/recommendations")]
public class RecommendationsController(IRecommendationService recommendationService) : ApiControllerBase
{
    [HttpGet("resources")]
    public async Task<IActionResult> GetResources(
        [FromQuery] string? dimensions,
        [FromQuery(Name = "position")] string? positionCode,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await recommendationService.GetResourcesAsync(User.GetUserId(), positionCode, dimensions, limit, cancellationToken);
        return Success(result, "查询成功");
    }

    [HttpGet("training-plan")]
    public async Task<IActionResult> GetTrainingPlan(
        [FromQuery] Guid? interviewId,
        [FromQuery] int weeks = 4,
        CancellationToken cancellationToken = default)
    {
        var result = await recommendationService.GetTrainingPlanAsync(User.GetUserId(), interviewId, weeks, cancellationToken);
        return Success(result, "查询成功");
    }
}

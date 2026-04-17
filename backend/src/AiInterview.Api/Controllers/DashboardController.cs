using AiInterview.Api.Extensions;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights(CancellationToken cancellationToken)
    {
        var result = await dashboardService.GetInsightsAsync(User.GetUserId(), cancellationToken);
        return Success(result, "获取个人画像能力概览成功");
    }
}

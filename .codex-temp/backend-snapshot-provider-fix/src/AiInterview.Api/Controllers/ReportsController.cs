using AiInterview.Api.Extensions;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Authorize]
[Route("api/v1/reports")]
public class ReportsController(IReportService reportService) : ApiControllerBase
{
    [HttpGet("{interviewId:guid}")]
    public async Task<IActionResult> GetReport([FromRoute] Guid interviewId, CancellationToken cancellationToken)
    {
        var result = await reportService.GetReportAsync(User.GetUserId(), interviewId, cancellationToken);
        return Success(result, "查询成功");
    }

    [HttpGet("growth")]
    public async Task<IActionResult> GetGrowth(
        [FromQuery(Name = "position")] string? positionCode,
        [FromQuery] string? timeRange,
        [FromQuery] string? dimension,
        CancellationToken cancellationToken)
    {
        var result = await reportService.GetGrowthAsync(User.GetUserId(), positionCode, timeRange, dimension, cancellationToken);
        return Success(result, "查询成功");
    }
}

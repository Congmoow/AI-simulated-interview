using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Route("api/v1/positions")]
public class PositionsController(IPositionService positionService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPositions(CancellationToken cancellationToken)
    {
        var result = await positionService.GetPositionsAsync(cancellationToken);
        return Success(result, "查询成功");
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetPositionDetail([FromRoute] string code, CancellationToken cancellationToken)
    {
        var result = await positionService.GetPositionDetailAsync(code, cancellationToken);
        return Success(result, "查询成功");
    }
}

using AiInterview.Api.DTOs.Knowledge;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Authorize]
[Route("api/v1/knowledge")]
public class KnowledgeController(IKnowledgeService knowledgeService) : ApiControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] KnowledgeSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await knowledgeService.SearchAsync(request, cancellationToken);
        return Success(result, "查询成功");
    }
}

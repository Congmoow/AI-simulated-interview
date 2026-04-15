using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Route("api/v1/questions")]
public class QuestionsController(IQuestionService questionService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetQuestions(
        [FromQuery(Name = "position")] string? positionCode,
        [FromQuery] string? type,
        [FromQuery] string? difficulty,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await questionService.GetQuestionsAsync(positionCode, type, difficulty, page, pageSize, cancellationToken);
        return Success(result, "查询成功");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuestionDetail([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await questionService.GetQuestionDetailAsync(id, cancellationToken);
        return Success(result, "查询成功");
    }
}

using AiInterview.Api.DTOs.Interviews;
using AiInterview.Api.Extensions;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Authorize]
[Route("api/v1/interviews")]
public class InterviewsController(IInterviewService interviewService) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateInterview([FromBody] CreateInterviewRequest request, CancellationToken cancellationToken)
    {
        var result = await interviewService.CreateInterviewAsync(User.GetUserId(), request, cancellationToken);
        return Success(result, "面试创建成功");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInterview([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await interviewService.GetInterviewAsync(User.GetUserId(), id, cancellationToken);
        return Success(result, "查询成功");
    }

    [HttpPost("{id:guid}/answers")]
    public async Task<IActionResult> SubmitAnswer([FromRoute] Guid id, [FromBody] SubmitAnswerRequest request, CancellationToken cancellationToken)
    {
        var result = await interviewService.SubmitAnswerAsync(User.GetUserId(), id, request, cancellationToken);
        return Success(result, "回答已提交");
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> FinishInterview([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await interviewService.FinishInterviewAsync(User.GetUserId(), id, cancellationToken);
        return Success(result, "面试已结束，报告生成中");
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery(Name = "position")] string? positionCode,
        [FromQuery] string? status,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await interviewService.GetHistoryAsync(User.GetUserId(), positionCode, status, startDate, endDate, page, pageSize, cancellationToken);
        return Success(result, "查询成功");
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<IActionResult> GetInterviewDetail([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await interviewService.GetInterviewDetailAsync(User.GetUserId(), id, cancellationToken);
        return Success(result, "查询成功");
    }
}

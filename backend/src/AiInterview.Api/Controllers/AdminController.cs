using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Admin;
using AiInterview.Api.Extensions;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[Route("api/v1/admin")]
public class AdminController(IAdminService adminService) : ApiControllerBase
{
    [HttpPost("questions")]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateQuestionRequest request, CancellationToken cancellationToken)
    {
        var result = await adminService.CreateQuestionAsync(request, cancellationToken);
        return Success(result, "题目创建成功");
    }

    [HttpPut("questions/{id:guid}")]
    public async Task<IActionResult> UpdateQuestion([FromRoute] Guid id, [FromBody] UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        var result = await adminService.UpdateQuestionAsync(id, request, cancellationToken);
        return Success(result, "题目更新成功");
    }

    [HttpPost("knowledge/documents")]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadKnowledgeDocument(
        [FromForm] UploadKnowledgeDocumentDto request,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var result = await adminService.UploadKnowledgeDocumentAsync(User.GetUserId(), request, file, cancellationToken);
        return Success(result, "文档上传成功，正在处理中");
    }

    [HttpGet("knowledge/documents")]
    public async Task<IActionResult> GetKnowledgeDocuments(
        [FromQuery] string? positionCode,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await adminService.GetKnowledgeDocumentsAsync(positionCode, status, page, pageSize, cancellationToken);
        return Success(result, "查询成功");
    }
}

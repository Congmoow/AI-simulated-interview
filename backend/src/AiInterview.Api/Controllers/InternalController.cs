using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Knowledge;
using AiInterview.Api.Middleware;
using AiInterview.Api.Options;
using AiInterview.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Controllers;

[AllowAnonymous]
[Route("api/v1/internal")]
public class InternalController(
    IAdminService adminService,
    IAiSettingsService aiSettingsService,
    IOptions<AiServiceOptions> aiServiceOptions,
    ILogger<InternalController> logger) : ApiControllerBase
{
    private readonly AiServiceOptions _aiServiceOptions = aiServiceOptions.Value;

    [HttpPost("knowledge/documents/{id:guid}/callback")]
    public async Task<IActionResult> DocumentCallback(
        [FromRoute] Guid id,
        [FromBody] DocumentProcessCallbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "未授权的内部调用" });
        }

        if (request.Status != "ready" && request.Status != "failed")
        {
            throw new AppException(ErrorCodes.DocumentProcessingFailed, "无效的回调状态值");
        }

        logger.LogInformation(
            "收到知识文档 {DocId} 处理回调，status={Status}，chunks={ChunkCount}",
            id,
            request.Status,
            request.Chunks.Count);

        await adminService.ProcessDocumentCallbackAsync(id, request, cancellationToken);
        return Success(new { documentId = id, status = request.Status }, "回调处理成功");
    }

    [HttpGet("ai/runtime-settings")]
    public async Task<IActionResult> GetAiRuntimeSettings(CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "未授权的内部调用" });
        }

        var result = await aiSettingsService.GetRuntimeSettingsAsync(cancellationToken);
        return Success(result, "查询成功");
    }

    private bool IsAuthorized()
    {
        var configuredApiKey = _aiServiceOptions.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            logger.LogWarning("AiService:ApiKey 未配置，拒绝所有内部请求");
            return false;
        }

        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        return authHeader == $"Bearer {configuredApiKey}";
    }
}

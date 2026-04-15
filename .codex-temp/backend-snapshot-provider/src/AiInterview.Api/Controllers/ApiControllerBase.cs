using AiInterview.Api.DTOs.Common;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Success<T>(T data, string message = "操作成功", int code = 200)
    {
        return Ok(ApiResponse<T>.Ok(data, message, code));
    }
}

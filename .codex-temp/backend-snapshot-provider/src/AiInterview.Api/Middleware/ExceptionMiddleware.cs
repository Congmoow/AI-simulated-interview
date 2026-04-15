using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Common;
using System.Text.Json;

namespace AiInterview.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException exception)
        {
            logger.LogWarning(exception, "业务异常: {Code} {Message}", exception.Code, exception.Message);
            await WriteErrorAsync(context, exception.StatusCode, exception.Code, exception.Message, exception.Errors);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "未处理异常");
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError,
                "服务器内部错误，请稍后重试");
        }
    }

    private static Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        int code,
        string message,
        IReadOnlyCollection<ApiError>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = ApiResponse<object?>.Fail(message, code, errors);
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}

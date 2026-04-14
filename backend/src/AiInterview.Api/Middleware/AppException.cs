using AiInterview.Api.DTOs.Common;

namespace AiInterview.Api.Middleware;

public class AppException : Exception
{
    public AppException(int code, string message, int statusCode = StatusCodes.Status400BadRequest, IReadOnlyCollection<ApiError>? errors = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Errors = errors;
    }

    public int Code { get; }

    public int StatusCode { get; }

    public IReadOnlyCollection<ApiError>? Errors { get; }
}

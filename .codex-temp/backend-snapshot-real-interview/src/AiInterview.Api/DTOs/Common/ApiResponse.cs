namespace AiInterview.Api.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }

    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public IReadOnlyCollection<ApiError>? Errors { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public static ApiResponse<T> Ok(T data, string message = "操作成功", int code = 200)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = code,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail(string message, int code, IReadOnlyCollection<ApiError>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Errors = errors
        };
    }
}

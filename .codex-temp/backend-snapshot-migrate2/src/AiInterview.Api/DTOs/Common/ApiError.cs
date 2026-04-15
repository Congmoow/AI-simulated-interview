namespace AiInterview.Api.DTOs.Common;

public class ApiError
{
    public string? Field { get; set; }

    public string Message { get; set; } = string.Empty;
}

namespace AiInterview.Api.DTOs.Common;

public class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

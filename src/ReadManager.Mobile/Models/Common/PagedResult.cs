using System.Text.Json.Serialization;

namespace ReadManager.Mobile.Models.Common;

// Wrapper chung cho các API trả danh sách có phân trang. Khớp PagedResultDto của API.
public class PagedResult<T>
{
    private int _totalPages;

    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // API trả tổng số ở trường "totalCount"
    [JsonPropertyName("totalCount")]
    public int TotalItems { get; set; }

    // API không trả TotalPages nên tự tính từ TotalCount / PageSize
    public int TotalPages
    {
        get => _totalPages > 0
            ? _totalPages
            : (PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 1);
        set => _totalPages = value;
    }
}

// Wrapper chung cho lỗi trả về từ API (khớp với ProblemDetails / lỗi tuỳ chỉnh của ASP.NET Core)
public class ApiError
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Errors { get; set; }
}

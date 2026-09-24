namespace ReadManager.Mobile.Models.Common;

// Wrapper chung cho các API trả danh sách có phân trang (danh sách truyện, danh sách chương...)
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

// Wrapper chung cho lỗi trả về từ API (khớp với ProblemDetails / lỗi tuỳ chỉnh của ASP.NET Core)
public class ApiError
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Errors { get; set; }
}

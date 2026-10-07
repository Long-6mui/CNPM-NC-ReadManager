namespace ReadManager.Api.DTOs.Stories;

// Bọc chung cho mọi API trả danh sách có phân trang.
// Generic để sau này Chapter/khác cũng dùng lại được, không chỉ riêng Story.
public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
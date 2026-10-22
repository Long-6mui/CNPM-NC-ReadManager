namespace ReadManager.Api.DTOs.Library;

// PB27 — Truyện thành viên đã mua riêng
public class PurchasedStoryDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }

    public decimal PricePaid { get; set; }          // giá lúc mua
    public DateTime PurchasedAt { get; set; }       // UTC

    // false = truyện hiện đang bị ẩn/gỡ; quyền vẫn được giữ nhưng chưa mở đọc được
    public bool IsAvailable { get; set; }
}

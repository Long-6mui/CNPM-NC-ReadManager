namespace ReadManager.Mobile.Models;

// Dùng cho màn Trang chủ / Danh sách truyện (danh sách rút gọn, load nhanh)
public class StorySummary
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string PublicationStatus { get; set; } = "Ongoing"; // Ongoing | Completed
    public string AccessPolicy { get; set; } = "Free";         // Free | Paid
    public List<string> GenreNames { get; set; } = new();
}

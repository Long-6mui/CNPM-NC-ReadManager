namespace ReadManager.Mobile.Models;

// Dùng cho màn Danh sách chương (chưa có nội dung, load nhanh)
public class ChapterSummary
{
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = "Free"; // Free | Paid — Sprint 1 chỉ mở khóa "Free"
    public DateTime? PublishedAt { get; set; }
}

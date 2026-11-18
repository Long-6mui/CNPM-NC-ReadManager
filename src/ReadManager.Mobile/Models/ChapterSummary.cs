using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// Dùng cho màn Danh sách chương (chưa có nội dung, load nhanh)
public class ChapterSummary
{
    public bool IsLocked { get; set; }
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = "Free"; // Free | Paid — chương Paid chỉ đọc được khi API trả IsLocked = false
    public DateTime? PublishedAt { get; set; }

    // Chỉ để hiển thị trong danh sách chương
    public string AccessText => IsLocked ? "🔒 Trả phí" : (AccessLevel == "Paid" ? "Đã mở khóa" : "Miễn phí");
    public Color AccessColor => IsLocked ? Color.FromArgb("#E5484D") : (AccessLevel == "Paid" ? Color.FromArgb("#12A150") : Color.FromArgb("#5B6B88"));
}

namespace ReadManager.Api.Entities;

// PB28 — TIẾN ĐỘ ĐỌC của 1 thành viên trên 1 truyện.
// Khóa chính = (UserId, StoryId) → mỗi người chỉ có 1 "điểm đọc tiếp" cho mỗi truyện.
// Lưu trên server (không lưu trên máy) → Web và Mobile dùng chung, đổi thiết bị vẫn đọc tiếp được.
public class ReadingProgress
{
    public int UserId { get; set; }
    public int StoryId { get; set; }

    // Chương đang đọc dở
    public int ChapterId { get; set; }

    // Vị trí cuộn trong chương, tính theo PHẦN TRĂM (0–100).
    // Dùng % thay vì pixel vì mỗi thiết bị có kích thước màn hình / cỡ chữ khác nhau.
    public decimal ScrollPercent { get; set; }

    // Lần lưu gần nhất (UTC) → dùng để sắp xếp "đang đọc" mới nhất lên đầu (PB29)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
}

namespace ReadManager.Api.Entities;

// PB31 — THÔNG BÁO gửi cho 1 người dùng (vd: "Truyện X vừa ra Chương 5").
// Chương hẹn giờ: CreatedAt = giờ ra mắt → chỉ hiện khi CreatedAt <= bây giờ.
public class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }          // người nhận
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }      // chương mới (null = thông báo chung)
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }         // đã đọc chưa
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
    public Chapter? Chapter { get; set; }
}

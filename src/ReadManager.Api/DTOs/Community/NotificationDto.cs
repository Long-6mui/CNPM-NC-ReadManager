namespace ReadManager.Api.DTOs.Community;

// PB31 — 1 THÔNG BÁO của người dùng
public class NotificationDto
{
    public int NotificationId { get; set; }
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }
    public int? ChapterNumber { get; set; }    // để bấm vào mở đúng chương
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }    // giờ UTC (chương hẹn giờ = giờ ra mắt)
}

// Danh sách thông báo + số chưa đọc (để hiện số trên chuông 🔔)
public class NotificationListDto
{
    public List<NotificationDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

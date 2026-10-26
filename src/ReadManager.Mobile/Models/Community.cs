using CommunityToolkit.Mvvm.ComponentModel;

namespace ReadManager.Mobile.Models;

// Sprint 3 — cộng đồng (Backend 3): bình luận, theo dõi truyện, thông báo, báo cáo.

public static class TimeAgo
{
    // API trả giờ UTC -> "vừa xong", "5 phút trước", "2 giờ trước", "3 ngày trước", hoặc ngày giờ đầy đủ
    public static string Of(DateTime utc)
    {
        var local = Fmt.Local(utc);
        var span = DateTime.Now - local;
        if (span.TotalSeconds < 0) return local.ToString("dd/MM/yyyy HH:mm");
        if (span.TotalMinutes < 1) return "vừa xong";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} phút trước";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} giờ trước";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
        return local.ToString("dd/MM/yyyy HH:mm");
    }
}

// GET api/stories/{id}/comments  (CommentDto)
public class CommentItem
{
    public int CommentId { get; set; }
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }
    public int? ChapterNumber { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool CanDelete { get; set; }

    public string Initial => string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName.Trim()[..1].ToUpperInvariant();
    public string ChapterText => ChapterNumber.HasValue ? $"Chương {ChapterNumber}" : "Bình luận chung";
    public string TimeText => TimeAgo.Of(CreatedAt);
}

public class CreateCommentRequest
{
    public int? ChapterId { get; set; }
    public string Content { get; set; } = string.Empty;
}

// GET/POST/DELETE api/stories/{id}/follow  (FollowStatusDto)
public class FollowStatus
{
    public int StoryId { get; set; }
    public bool IsFollowing { get; set; }
    public int FollowerCount { get; set; }
}

// GET api/me/follows  (FollowedStoryDto)
public class FollowedStory
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public DateTime FollowedAt { get; set; }

    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    public bool HasNoCover => !HasCover;
    public string FollowedText => $"Theo dõi {TimeAgo.Of(FollowedAt)}";
}

// GET api/notifications  (NotificationDto) — IsRead đổi được để giao diện cập nhật ngay khi bấm
public partial class AppNotification : ObservableObject
{
    public int NotificationId { get; set; }
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }
    public int? ChapterNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnread))]
    private bool isRead;

    public bool IsUnread => !IsRead;
    public string TimeText => TimeAgo.Of(CreatedAt);
    public string TargetText => ChapterNumber.HasValue ? $"Chương {ChapterNumber}" : "Xem truyện";
}

public class NotificationList
{
    public List<AppNotification> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
}

// POST api/reports  (CreateReportDto)
public class ReportRequest
{
    public string TargetType { get; set; } = string.Empty;   // Story | Chapter | Comment
    public int TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
}

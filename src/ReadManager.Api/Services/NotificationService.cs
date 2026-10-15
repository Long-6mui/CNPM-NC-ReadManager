using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// PB31 — THÔNG BÁO CHƯƠNG MỚI
public interface INotificationService
{
    // ChapterService gọi sau khi tạo / sửa / tải lên chương → tạo thông báo cho người theo dõi.
    Task SyncChaptersAsync(IEnumerable<int> chapterIds);

    Task<NotificationListDto> GetListAsync(int userId, int page, int pageSize);
    Task<int> GetUnreadCountAsync(int userId);
    Task<bool> MarkReadAsync(int userId, int notificationId);
    Task MarkAllReadAsync(int userId);
}

public class NotificationService : INotificationService
{
    private const string Published = "Published";
    private const string PublicVisibility = "Public";
    private const int MaxPageSize = 50;

    // Chỉ báo "chương mới" khi chương vừa ra mắt trong vòng 10 phút (hoặc hẹn giờ ở tương lai).
    // → Admin sửa lỗi chính tả 1 chương cũ thì KHÔNG gửi thông báo lại.
    private static readonly TimeSpan FreshWindow = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    // ===== TẠO / CẬP NHẬT THÔNG BÁO CHO CÁC CHƯƠNG =====
    // Cách làm với chương HẸN GIỜ: tạo thông báo sẵn với CreatedAt = giờ ra mắt.
    // Khi lấy danh sách chỉ lọc CreatedAt <= bây giờ → tới giờ thông báo tự hiện, không cần chạy nền.
    public async Task SyncChaptersAsync(IEnumerable<int> chapterIds)
    {
        var ids = chapterIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var now = DateTime.UtcNow;

        // 1. Xóa thông báo CHƯA tới giờ hiện của các chương này
        //    (admin đổi giờ hẹn / chuyển về nháp → thông báo cũ không còn đúng)
        await _db.Notifications
            .Where(n => n.ChapterId != null && ids.Contains(n.ChapterId.Value) && n.CreatedAt > now)
            .ExecuteDeleteAsync();

        // 2. Lấy thông tin các chương đang công khai, thuộc truyện công khai
        var chapters = await _db.Chapters.AsNoTracking()
            .Where(c => ids.Contains(c.ChapterId)
                     && c.PublicationStatus == Published
                     && c.Story.Visibility == PublicVisibility)
            .Select(c => new
            {
                c.ChapterId, c.StoryId, c.ChapterNumber, c.Title,
                c.ScheduledAt, c.PublishedAt,
                StoryTitle = c.Story.Title
            })
            .ToListAsync();

        // 3. Chương nào ĐÃ có thông báo rồi (đã hiện trước đó) → không tạo lại
        var alreadyNotified = (await _db.Notifications
                .Where(n => n.ChapterId != null && ids.Contains(n.ChapterId.Value))
                .Select(n => n.ChapterId!.Value)
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        // 4. Người theo dõi của các truyện liên quan
        var storyIds = chapters.Select(c => c.StoryId).Distinct().ToList();
        var followers = (await _db.StoryFollows.AsNoTracking()
                .Where(f => storyIds.Contains(f.StoryId))
                .Select(f => new { f.StoryId, f.UserId })
                .ToListAsync())
            .ToLookup(f => f.StoryId, f => f.UserId);

        // 5. Tạo thông báo
        foreach (var c in chapters)
        {
            if (alreadyNotified.Contains(c.ChapterId))
                continue;

            var releaseAt = c.ScheduledAt ?? c.PublishedAt ?? now;   // giờ ra mắt (hẹn giờ → giờ hẹn)
            if (releaseAt < now - FreshWindow)
                continue;   // chương cũ, chỉ đang được sửa → bỏ qua

            var message = $"\"{c.StoryTitle}\" vừa ra Chương {c.ChapterNumber}: {c.Title}";
            if (message.Length > 500)
                message = message[..497] + "...";

            foreach (var userId in followers[c.StoryId])
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    StoryId = c.StoryId,
                    ChapterId = c.ChapterId,
                    Message = message,
                    IsRead = false,
                    CreatedAt = releaseAt
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    // ===== DANH SÁCH THÔNG BÁO CỦA TÔI =====
    public async Task<NotificationListDto> GetListAsync(int userId, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var now = DateTime.UtcNow;

        // Chỉ lấy thông báo đã tới giờ hiện
        var query = _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.CreatedAt <= now);

        var total = await query.CountAsync();
        var unread = await query.CountAsync(n => !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                StoryId = n.StoryId,
                ChapterId = n.ChapterId,
                ChapterNumber = n.Chapter != null ? n.Chapter.ChapterNumber : null,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new NotificationListDto
        {
            Items = items,
            TotalCount = total,
            UnreadCount = unread,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        var now = DateTime.UtcNow;
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead && n.CreatedAt <= now);
    }

    // Trả false = không có thông báo này (hoặc không phải của mình)
    public async Task<bool> MarkReadAsync(int userId, int notificationId)
    {
        var changed = await _db.Notifications
            .Where(n => n.NotificationId == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return changed > 0;
    }

    public async Task MarkAllReadAsync(int userId)
    {
        var now = DateTime.UtcNow;
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && n.CreatedAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}

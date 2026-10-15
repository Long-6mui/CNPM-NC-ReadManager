using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — PB31: THÔNG BÁO CHƯƠNG MỚI (tất cả đều phải đăng nhập)
//   GET api/notifications?page=1          → thông báo của tôi (mới nhất ở trên) + số chưa đọc
//   GET api/notifications/unread-count    → chỉ số chưa đọc (cho chuông 🔔)
//   PUT api/notifications/12/read         → đánh dấu 1 thông báo đã đọc
//   PUT api/notifications/read-all        → đánh dấu tất cả đã đọc
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("api/notifications")]
    public async Task<ActionResult<NotificationListDto>> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _notificationService.GetListAsync(CurrentUserId, page, pageSize));

    [HttpGet("api/notifications/unread-count")]
    public async Task<ActionResult<int>> UnreadCount()
        => Ok(await _notificationService.GetUnreadCountAsync(CurrentUserId));

    [HttpPut("api/notifications/{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId)
        => await _notificationService.MarkReadAsync(CurrentUserId, notificationId)
            ? NoContent()
            : NotFound(new { message = "Không tìm thấy thông báo." });

    [HttpPut("api/notifications/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkAllReadAsync(CurrentUserId);
        return NoContent();
    }
}

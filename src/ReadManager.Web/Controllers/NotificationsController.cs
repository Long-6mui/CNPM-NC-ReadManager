using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Controllers;

// BACKEND 4 — PB31: TRANG "THÔNG BÁO CỦA TÔI" (bấm vào chuông 🔔 trên thanh menu)
[Authorize]
public class NotificationsController(StoriesApiClient api) : Controller
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");

    // GET /Notifications?page=1
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var data = await api.NotificationsAsync(Math.Max(page, 1), await Token());
            return View(new NotificationsVm
            {
                Items = data.Items,
                TotalCount = data.TotalCount,
                UnreadCount = data.UnreadCount,
                Page = data.Page,
                PageCount = Math.Max(1, (int)Math.Ceiling(data.TotalCount / (double)data.PageSize))
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return View(new NotificationsVm());
        }
    }

    // Bấm vào 1 thông báo → đánh dấu đã đọc rồi mở đúng chương
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(int id, int storyId, int? chapterNumber)
    {
        try { await api.MarkNotificationReadAsync(id, await Token()); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }

        return chapterNumber.HasValue
            ? RedirectToAction("Read", "Stories", new { storyId, no = chapterNumber })
            : RedirectToAction("Details", "Stories", new { id = storyId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll()
    {
        try
        {
            await api.MarkAllNotificationsReadAsync(await Token());
            TempData["Toast"] = "Đã đánh dấu tất cả là đã đọc.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return RedirectToAction(nameof(Index));
    }
}

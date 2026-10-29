using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Controllers;

public class LibraryVm
{
    public string? ReadingError { get; set; }
    public string Tab { get; set; } = "reading";
    public List<ReadingStoryApi> Reading { get; set; } = [];     // PB28 + PB29
    public List<FollowedStoryApi> Following { get; set; } = [];  // PB30
}

// "Thư viện của tôi": truyện đang đọc dở (PB28–29) và truyện đang theo dõi (PB30)
[Authorize]
public class LibraryController(StoriesApiClient api, ReadingProgressApiClient progress) : Controller
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";

    public async Task<IActionResult> Index(string tab = "reading")
    {
        var vm = new LibraryVm { Tab = tab == "following" ? "following" : "reading" };
        try { vm.Reading = await progress.ListAsync(await HttpContext.GetTokenAsync("access_token")); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { vm.ReadingError = ReadingProgressApiClient.ErrorMessage(ex); }
        try { vm.Following = await api.MyFollowsAsync(await HttpContext.GetTokenAsync("access_token")); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return View(vm);
    }

    // Xóa truyện khỏi danh sách "Đang đọc" (không ảnh hưởng quyền đọc hay theo dõi)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProgress(int storyId)
    {
        try
        {
            TempData["Toast"] = await progress.RemoveAsync(storyId, await HttpContext.GetTokenAsync("access_token"))
                ? "Đã xóa khỏi danh sách đang đọc trên các thiết bị." : "Truyện không còn trong danh sách.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { TempData["Toast"] = ReadingProgressApiClient.ErrorMessage(ex); }
        return RedirectToAction(nameof(Index), new { tab = "reading" });
    }

    // Browser gửi form cùng antiforgery token; API xác định user từ access token của phiên Web.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProgress(int storyId, int chapterId, decimal scrollPercent)
    {
        if (!ModelState.IsValid || storyId <= 0 || chapterId <= 0 || scrollPercent < 0 || scrollPercent > 100)
            return BadRequest(new { message = "Tiến độ đọc không hợp lệ." });
        try
        {
            await progress.SaveAsync(storyId, chapterId, scrollPercent, await HttpContext.GetTokenAsync("access_token"));
            return Ok(new { saved = true });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var status = ex is HttpRequestException { StatusCode: not null } error ? (int)error.StatusCode.Value : 503;
            return StatusCode(status, new { message = ReadingProgressApiClient.ErrorMessage(ex) });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unfollow(int storyId)
    {
        try { TempData["Toast"] = (await api.SetFollowAsync(storyId, false, await HttpContext.GetTokenAsync("access_token"))).Message; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index), new { tab = "following" });
    }
}

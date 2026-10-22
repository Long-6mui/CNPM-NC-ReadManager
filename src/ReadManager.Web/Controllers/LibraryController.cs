using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Controllers;

public class LibraryVm
{
    public string Tab { get; set; } = "reading";
    public List<ReadingProgress> Reading { get; set; } = [];     // PB28 + PB29
    public List<FollowedStoryApi> Following { get; set; } = [];  // PB30
}

// "Thư viện của tôi": truyện đang đọc dở (PB28–29) và truyện đang theo dõi (PB30)
[Authorize]
public class LibraryController(StoriesApiClient api, DemoReadingProgressStore progress) : Controller
{
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";

    public async Task<IActionResult> Index(string tab = "reading")
    {
        var vm = new LibraryVm { Tab = tab == "following" ? "following" : "reading", Reading = progress.List(Uid) };
        try { vm.Following = await api.MyFollowsAsync(await HttpContext.GetTokenAsync("access_token")); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return View(vm);
    }

    // Xóa truyện khỏi danh sách "Đang đọc" (không ảnh hưởng quyền đọc hay theo dõi)
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult RemoveProgress(int storyId)
    {
        TempData["Toast"] = progress.Remove(Uid, storyId) ? "Đã xóa khỏi danh sách đang đọc." : "Truyện không còn trong danh sách.";
        return RedirectToAction(nameof(Index), new { tab = "reading" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unfollow(int storyId)
    {
        try { TempData["Toast"] = (await api.SetFollowAsync(storyId, false, await HttpContext.GetTokenAsync("access_token"))).Message; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index), new { tab = "following" });
    }
}

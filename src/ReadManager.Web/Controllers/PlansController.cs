using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Controllers;

public class PlansController(PlansApiClient plans, StoriesApiClient stories) : Controller
{
    private const string ApiDown = "Không tải được dữ liệu từ API. Vui lòng thử lại.";
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");

    public async Task<IActionResult> Index()
    {
        try
        {
            var list = (await plans.ListAsync()).Where(p => p.IsAvailable && p.CurrentVersion is not null).ToList();
            var titles = new Dictionary<int, string>();
            foreach (var id in list.Where(p => p.CurrentVersion!.ScopeType == "Selected").SelectMany(p => p.CurrentVersion!.StoryIds).Distinct())
            {
                var story = await stories.GetStoryAsync(id);
                if (story is not null) titles[id] = story.Title;
            }
            ViewBag.StoryTitles = titles;
            return View("PlansIndex", list);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return View("PlansIndex", new List<ReadingPlanApi>());
        }
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(int planId, int planVersionId)
    {
        if (planId <= 0 || planVersionId <= 0)
        {
            TempData["Toast"] = "Vui lòng tải lại danh sách gói rồi thử lại.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var result = await stories.CreatePlanOrderAsync(planId, planVersionId, await Token());
            if (result.Ok && result.Id.HasValue)
                return RedirectToAction("Details", "Orders", new { id = result.Id.Value });
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyStory(int storyId)
    {
        try
        {
            var result = await stories.CreateStoryOrderAsync(storyId, await Token());
            if (result.Ok && result.Id.HasValue)
                return RedirectToAction("Details", "Orders", new { id = result.Id.Value });
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction("Details", "Stories", new { id = storyId });
    }

    [Authorize]
    public async Task<IActionResult> Mine(int page = 1)
    {
        try { return View(await stories.MyOrdersAsync(await Token(), page)); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return View(new PagedApi<OrderApi>([], 0, 1, 20));
        }
    }
}

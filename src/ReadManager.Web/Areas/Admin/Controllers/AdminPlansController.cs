using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Areas.Admin.Controllers;

public class AdminPlansVm
{
    public List<ReadingPlanApi> Plans { get; set; } = [];
    public List<StoryListItemApi> AllStories { get; set; } = [];
    public Dictionary<int, List<PlanVersionApi>> Versions { get; set; } = [];
}

// PB17 + PB18 — quản lý gói đọc, gọi API thật (api/reading-plans). Chỉ Admin (kế thừa PreviewController).
[Area("Admin")]
public class PlansController(IWebHostEnvironment environment, PlansApiClient api, StoriesApiClient stories) : PreviewController(environment)
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");

    public async Task<IActionResult> Index()
    {
        var vm = new AdminPlansVm();
        try
        {
            var token = await Token();
            vm.Plans = await api.AdminListAsync(token);
            vm.AllStories = (await stories.AdminListAsync(token)).Items;
            foreach (var p in vm.Plans) vm.Versions[p.PlanId] = await api.VersionsAsync(p.PlanId, token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return View("AdminPlansIndex", vm);
    }

    // planId = 0: tạo gói mới. planId > 0: phát hành phiên bản mới (giá/thời hạn/phạm vi); đơn cũ giữ giá cũ.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int planId, string? name, decimal price, int months, string scope, List<int>? storyIds)
    {
        storyIds ??= [];
        if (scope == "All") storyIds = [];
        string? error = null;
        if (planId == 0 && string.IsNullOrWhiteSpace(name)) error = "Vui lòng nhập tên gói.";
        else if (price <= 0) error = "Giá phải lớn hơn 0.";
        else if (months < 1 || months > 120) error = "Thời hạn từ 1 đến 120 tháng.";
        else if (scope != "All" && scope != "Selected") error = "Phạm vi không hợp lệ.";
        else if (scope == "Selected" && storyIds.Count == 0) error = "Gói \"Truyện được chọn\" cần chọn ít nhất một truyện.";
        if (error is not null) { TempData["Toast"] = error; return RedirectToAction(nameof(Index)); }

        try
        {
            var token = await Token();
            var result = planId == 0
                ? await api.CreateAsync(name!.Trim(), price, months, scope, storyIds, token)
                : await api.AddVersionAsync(planId, price, months, scope, storyIds, token);
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int planId, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) { TempData["Toast"] = "Vui lòng nhập tên gói."; return RedirectToAction(nameof(Index)); }
        try { TempData["Toast"] = (await api.RenameAsync(planId, name.Trim(), await Token())).Message; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(int planId, bool isAvailable)
    {
        try { TempData["Toast"] = (await api.SetAvailabilityAsync(planId, isAvailable, await Token())).Message; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index));
    }
}

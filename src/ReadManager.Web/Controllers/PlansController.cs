using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Controllers;

public record MineVm(List<PlanEntitlement> Plans, List<StoryEntitlement> Stories, List<OrderItem> Orders);

public class PlansController(PlansApiClient plans, StoriesApiClient stories, DemoPlanStore store) : Controller
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string UserName => User.Identity?.Name ?? "Thành viên";

    // PB17 — danh sách gói đang bán (lấy từ API, ai cũng xem được)
    public async Task<IActionResult> Index()
    {
        try
        {
            var list = (await plans.ListAsync()).Where(p => p.IsAvailable && p.CurrentVersion is not null).ToList();
            // Gói Selected: lấy tên truyện để hiện "Áp dụng cho: A, B"
            var titles = new Dictionary<int, string>();
            foreach (var id in list.Where(p => p.CurrentVersion!.ScopeType == "Selected").SelectMany(p => p.CurrentVersion!.StoryIds).Distinct())
            {
                var s = await stories.GetStoryAsync(id);
                if (s is not null) titles[id] = s.Title;
            }
            ViewBag.StoryTitles = titles;
            ViewBag.Active = Uid == "" ? new List<PlanEntitlement>() : store.PlanEntitlements(Uid);
            return View("PlansIndex", list);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return View("PlansIndex", new List<ReadingPlanApi>());
        }
    }

    // PB19 + PB20 — mua / gia hạn gói. Giá luôn lấy từ API ở thời điểm tạo đơn, không tin dữ liệu form.
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(int planId)
    {
        try
        {
            var plan = await plans.GetAsync(planId);   // gói ngừng bán → API trả 404
            if (plan is null || !plan.IsAvailable || plan.CurrentVersion is null)
            {
                TempData["Toast"] = "Gói này hiện không còn bán.";
                return RedirectToAction(nameof(Index));
            }
            store.CreatePlanOrder(Uid, UserName, plan);
            TempData["Toast"] = "Đã tạo đơn hàng. Hãy hoàn tất thanh toán.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; return RedirectToAction(nameof(Index)); }
        return RedirectToAction(nameof(Mine));
    }

    // PB21 — mua quyền đọc riêng một bộ truyện (độc lập với gói tháng)
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyStory(int storyId)
    {
        try
        {
            var s = await stories.GetStoryAsync(storyId);
            if (s is null) return NotFound();
            if (s.AccessPolicy == "Free" || (s.CurrentPrice ?? 0) <= 0)
            {
                TempData["Toast"] = "Truyện này miễn phí hoặc chưa bán riêng.";
                return RedirectToAction("Details", "Stories", new { id = storyId });
            }
            var order = store.CreateStoryOrder(Uid, UserName, storyId, s.Title, s.CurrentPrice!.Value);
            if (order is null)
            {
                TempData["Toast"] = "Bạn đã sở hữu truyện này, không cần thanh toán lại.";
                return RedirectToAction("Details", "Stories", new { id = storyId });
            }
            TempData["Toast"] = "Đã tạo đơn mua truyện. Hãy hoàn tất thanh toán.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; return RedirectToAction("Details", "Stories", new { id = storyId }); }
        return RedirectToAction(nameof(Mine));
    }

    // PB26 + PB27 — gói đang dùng, truyện đã mua, lịch sử thanh toán
    [Authorize] public IActionResult Mine() => View(new MineVm(store.PlanEntitlements(Uid), store.OwnedStories(Uid), store.Orders(Uid)));

    // Giả lập kết quả cổng thanh toán (PB24). outcome = ok | fail | grantfail
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult Pay(int orderId, string outcome)
    {
        var done = store.Pay(Uid, orderId, outcome);
        TempData["Toast"] = !done ? "Không xử lý được đơn này (đã xử lý trước đó?)."
            : outcome == "ok" ? "Thanh toán thành công, quyền đọc đã được cấp."
            : outcome == "grantfail" ? "Thanh toán thành công nhưng cập nhật quyền bị lỗi. Quản trị viên sẽ khôi phục, bạn không bị thu thêm tiền."
            : "Thanh toán thất bại, chưa cấp quyền đọc.";
        return RedirectToAction(nameof(Mine));
    }
}

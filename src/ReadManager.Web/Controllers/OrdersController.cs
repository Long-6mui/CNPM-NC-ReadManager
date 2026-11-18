using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Controllers;

// PB21 — MUA TRỌN BỘ 1 TRUYỆN (Web)
//   GET  /Orders/Checkout?storyId=4  → trang "Mua trọn bộ": thông tin truyện, giá, quyền lợi + nút Thanh toán / Quay lại
//   POST /Orders/Pay                 → bấm "Thanh toán": tạo đơn (giá lấy từ DB) rồi mở trang đơn hàng
//   GET  /Orders/Details/12          → xem đơn: món hàng, số tiền, trạng thái
// Hủy đơn / xác nhận thanh toán: phần Thanh toán (BE4 — feature/s2-payments) làm.
[Authorize]
public class OrdersController(StoriesApiClient api, PaymentsApiClient payments, IWebHostEnvironment environment) : Controller
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");

    public async Task<IActionResult> Checkout(int storyId)
    {
        try
        {
            var detail = await api.GetStoryAsync(storyId);
            var quote = await api.QuoteStoryAsync(storyId, await Token());
            if (detail is null || quote is null)
            {
                TempData["Toast"] = "Không tìm thấy truyện.";
                return RedirectToAction("Index", "Home");
            }

            // Đã mua rồi / truyện miễn phí → không cần mua, quay về trang truyện
            if (!quote.CanBuy)
            {
                TempData["Toast"] = quote.Message ?? "Truyện này không cần mua.";
                return RedirectToAction("Details", "Stories", new { id = storyId });
            }

            return View(new CheckoutVm
            {
                Story = StoriesApiClient.ToStory(detail),
                TotalChapters = detail.PublishedChapterCount,
                Quote = quote
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return RedirectToAction("Details", "Stories", new { id = storyId });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int storyId)
    {
        try
        {
            var result = await api.CreateStoryOrderAsync(storyId, await Token());
            if (result.Ok && result.Id.HasValue)
                return RedirectToAction(nameof(Details), new { id = result.Id });

            TempData["Toast"] = result.Message;   // vd: "Bạn đã sở hữu truyện này."
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return RedirectToAction(nameof(Checkout), new { storyId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPayment(int id)
    {
        try
        {
            var result = await payments.StartAsync(id, await Token());
            if (result.Ok && result.Data is not null) return View("Payment", result.Data);
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SimulatePayment(int id, string status)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (status is not ("Success" or "Failed" or "Cancelled" or "Pending")) return BadRequest();
        try
        {
            var result = await payments.SimulateAsync(id, status, await Token());
            TempData["Toast"] = result.Ok ? result.Data!.Message : result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var order = await api.GetOrderAsync(id, await Token());
            if (order is null)
            {
                TempData["Toast"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index", "Home");
            }
            return View(order);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return RedirectToAction("Index", "Home");
        }
    }
}

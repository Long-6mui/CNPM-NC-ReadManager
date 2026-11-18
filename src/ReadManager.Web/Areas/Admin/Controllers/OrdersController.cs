using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class OrdersController(IWebHostEnvironment environment, StoriesApiClient api, PaymentsApiClient payments) : PreviewController(environment)
{
    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        ViewBag.Status = status;
        try { return View(await api.AdminOrdersAsync(await HttpContext.GetTokenAsync("access_token"), status, page)); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = "Không tải được đơn hàng từ API. Vui lòng thử lại.";
            return View(new PagedApi<OrderApi>([], 0, 1, 20));
        }
    }
    public async Task<IActionResult> Recovery(int page = 1)
    {
        try
        {
            var result = await payments.RecoveryAsync(page, await HttpContext.GetTokenAsync("access_token"));
            if (result.Ok) return View(result.Data);
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { TempData["Toast"] = "Không tải được danh sách cần cấp lại quyền. Vui lòng thử lại."; }
        return View(new PagedApi<PaymentRecoveryApi>([], 0, 1, 20));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Regrant(int id)
    {
        try
        {
            var result = await payments.RegrantAsync(id, await HttpContext.GetTokenAsync("access_token"));
            TempData["Toast"] = result.Ok ? result.Data!.Message : result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { TempData["Toast"] = "Không cấp lại được quyền qua API. Vui lòng thử lại."; }
        return RedirectToAction(nameof(Recovery));
    }
}

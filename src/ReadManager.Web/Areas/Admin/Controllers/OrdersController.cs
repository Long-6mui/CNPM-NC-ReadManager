using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class OrdersController(IWebHostEnvironment environment, StoriesApiClient api) : PreviewController(environment)
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
}

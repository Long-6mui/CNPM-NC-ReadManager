using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Areas.Admin.Controllers;

// BACKEND 4 — PB33: TRANG ADMIN "BÁO CÁO VI PHẠM"
// Kế thừa PreviewController → đã có [Authorize(Roles = "Admin")], chỉ Admin vào được.
[Area("Admin")]
public class ReportsController(IWebHostEnvironment environment, StoriesApiClient api) : PreviewController(environment)
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");

    // GET /Admin/Reports?status=Pending&page=1   (status rỗng = tất cả)
    public async Task<IActionResult> Index(string? status = "Pending", int page = 1)
    {
        var vm = new AdminReportsVm { Status = string.IsNullOrEmpty(status) ? null : status, Page = Math.Max(page, 1) };
        try
        {
            var data = await api.AdminReportsAsync(vm.Status, vm.Page, await Token());
            vm.Items = data.Items;
            vm.TotalCount = data.TotalCount;
            vm.PageCount = Math.Max(1, (int)Math.Ceiling(data.TotalCount / (double)data.PageSize));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return View(vm);
    }

    // Xử lý 1 báo cáo: decision = "Resolved" (vi phạm) | "Rejected" (bác bỏ)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Handle(int id, string decision, bool hideComment, string? status, int page = 1)
    {
        try
        {
            var result = await api.HandleReportAsync(id, decision, hideComment, await Token());
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return RedirectToAction(nameof(Index), new { status, page });
    }
}

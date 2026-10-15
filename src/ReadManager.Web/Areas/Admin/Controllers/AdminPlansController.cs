using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class PlansController(IWebHostEnvironment environment, DemoPlanStore store) : PreviewController(environment)
{
    public IActionResult Index() => View("AdminPlansIndex", store.Latest());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(int planId, string? name, decimal price, int months, string scope, bool available)
    {
        if (string.IsNullOrWhiteSpace(name) || price <= 0 || months <= 0 || (scope != "All" && scope != "Selected"))
            TempData["Toast"] = "Nhập tên gói, giá lớn hơn 0 và thời hạn lớn hơn 0 tháng.";
        else
        {
            store.Save(planId, name.Trim(), price, months, scope, available);
            TempData["Toast"] = planId == 0 ? "Đã tạo gói mới." : "Đã lưu: phát hành phiên bản mới của gói.";
        }
        return RedirectToAction(nameof(Index));
    }
}

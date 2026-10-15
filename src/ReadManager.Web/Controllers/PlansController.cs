using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Controllers;

public record MineVm(List<Entitlement> Entitlements, List<OrderItem> Orders);

public class PlansController(DemoPlanStore store) : Controller
{
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    // Danh sách gói đọc (ai cũng xem được)
    public IActionResult Index() => View("PlansIndex", store.Latest(onlyAvailable: true));

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult Buy(int versionId)
    {
        var order = store.Buy(Uid, versionId);
        TempData["Toast"] = order is null ? "Gói này hiện không còn bán." : "Đã tạo đơn hàng. Hãy hoàn tất thanh toán.";
        return RedirectToAction(nameof(Mine));
    }

    // Gói đang có, trạng thái giao dịch và lịch sử thanh toán
    [Authorize] public IActionResult Mine() => View(new MineVm(store.Entitlements(Uid), store.Orders(Uid)));

    // Giả lập kết quả cổng thanh toán (thật sự do Backend 4 xử lý)
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult Pay(int orderId, bool ok)
    {
        TempData["Toast"] = store.Pay(Uid, orderId, ok)
            ? (ok ? "Thanh toán thành công, quyền đọc đã được cấp." : "Thanh toán thất bại, chưa cấp quyền đọc.")
            : "Không xử lý được đơn này.";
        return RedirectToAction(nameof(Mine));
    }
}

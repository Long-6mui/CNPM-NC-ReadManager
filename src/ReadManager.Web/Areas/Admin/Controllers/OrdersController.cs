using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Areas.Admin.Controllers;

// PB25 — Admin xem giao dịch và khôi phục quyền đọc khi "thanh toán thành công nhưng cập nhật quyền bị lỗi".
[Area("Admin")]
public class OrdersController(IWebHostEnvironment environment, DemoPlanStore store) : PreviewController(environment)
{
    public IActionResult Index(string? status)
    {
        ViewBag.Status = status;
        return View(store.AllOrders(status));
    }

    // Idempotent: chỉ khôi phục đơn Paid còn thiếu quyền; bấm lại không cấp trùng, không thu thêm tiền.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Recover(int id, string? status)
    {
        TempData["Toast"] = store.Recover(id)
            ? $"Đã khôi phục quyền đọc cho đơn #{id}. Không thu thêm tiền."
            : $"Đơn #{id} không cần khôi phục (đã đủ quyền hoặc chưa thanh toán thành công).";
        return RedirectToAction(nameof(Index), new { status });
    }
}

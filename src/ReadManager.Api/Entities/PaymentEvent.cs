namespace ReadManager.Api.Entities;

// BE4 — NHẬT KÝ THANH TOÁN (audit log)
// Ghi mọi callback hợp lệ và mọi lần cấp quyền (thành công / lỗi) để đối chiếu khi có khiếu nại.
// OrderId KHÔNG có khóa ngoại: callback cho đơn không tồn tại vẫn phải ghi lại được.
//
// EventType: CALLBACK_PAID | CALLBACK_PAIDGRANTFAILED | CALLBACK_DUPLICATE | CALLBACK_FAILED | CALLBACK_CANCELLED |
//            CALLBACK_PENDING | CALLBACK_ORDERNOTFOUND | CALLBACK_AMOUNTMISMATCH | GRANT_OK | GRANT_FAILED
public class PaymentEvent
{
    public int PaymentEventId { get; set; }
    public int? OrderId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
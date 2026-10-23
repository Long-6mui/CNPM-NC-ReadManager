namespace ReadManager.Api.Entities;

// BE4 — GIAO DỊCH THANH TOÁN (PB19, PB21, PB24, PB25)
// Mỗi lần cổng thanh toán báo kết quả CUỐI CÙNG (thành công / thất bại / hủy) cho 1 đơn → ghi 1 dòng.
// (Provider, GatewayTransactionId) là DUY NHẤT → callback gửi lặp sẽ bị database chặn, không xử lý lại.
//
// Status:      "Success" | "Failed" | "Cancelled"
// GrantStatus: (chỉ có ý nghĩa khi Status = "Success")
//   "NotRequired" = giao dịch không thành công → không cấp quyền
//   "Pending"     = đã thu tiền, chưa cấp quyền xong
//   "Granted"     = đã cấp quyền
//   "Failed"      = đã thử cấp quyền nhưng lỗi → Admin chạy lại (PB25)
public class PaymentTransaction
{
    public int PaymentTransactionId { get; set; }
    public int OrderId { get; set; }

    public string Provider { get; set; } = string.Empty;               // "Mock", "VNPay", ...
    public string GatewayTransactionId { get; set; } = string.Empty;   // mã giao dịch phía cổng thanh toán

    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string GrantStatus { get; set; } = "NotRequired";
    public int GrantAttempts { get; set; }
    public string? GrantError { get; set; }

    public Order Order { get; set; } = null!;
}
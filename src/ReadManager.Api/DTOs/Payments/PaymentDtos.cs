using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Payments;

// CALLBACK do cổng thanh toán gửi về server (server → server, KHÔNG có đăng nhập của người dùng).
// Cổng thật (VNPay/MoMo...) đặt tên trường khác → chỉ cần đổi cách map + cách tính chữ ký.
public class PaymentCallbackDto
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required, StringLength(64)]
    public string TransactionId { get; set; } = string.Empty;   // mã giao dịch phía cổng thanh toán

    public decimal Amount { get; set; }

    [Required, RegularExpression("^(Success|Failed|Cancelled|Pending)$",
        ErrorMessage = "Status phải là Success, Failed, Cancelled hoặc Pending.")]
    public string Status { get; set; } = string.Empty;

    [Required]
    public string Signature { get; set; } = string.Empty;       // HMAC-SHA256 (hex)
}

// Trả lời cổng thanh toán: HTTP 200 = "đã nhận, đừng gửi lại"
public class PaymentCallbackResultDto
{
    public string Result { get; set; } = string.Empty;   // Paid | PaidGrantFailed | Duplicate | Failed | Cancelled | Pending | InvalidSignature | OrderNotFound | AmountMismatch
    public string Message { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public bool EntitlementGranted { get; set; }
}

// 1 dòng trong danh sách Admin cần xử lý (PB25)
public class PaymentTransactionDto
{
    public int PaymentTransactionId { get; set; }
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string GatewayTransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GrantStatus { get; set; } = string.Empty;
    public int GrantAttempts { get; set; }
    public string? GrantError { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Kết quả Admin chạy lại cấp quyền
public class RegrantResultDto
{
    public int OrderId { get; set; }
    public bool Granted { get; set; }          // true = đơn này giờ đã có quyền đọc
    public bool AlreadyGranted { get; set; }   // true = trước đó đã cấp rồi → lần chạy lại này KHÔNG cấp thêm
    public int? EntitlementId { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

// CHỈ DÙNG KHI THỬ (Development): giả lập cổng thanh toán báo kết quả
public class SimulatePaymentDto
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required, RegularExpression("^(Success|Failed|Cancelled|Pending)$")]
    public string Status { get; set; } = "Success";

    [StringLength(64)]
    public string? TransactionId { get; set; }   // bỏ trống = tự sinh mã mới; gửi lại CÙNG mã để thử callback lặp
}
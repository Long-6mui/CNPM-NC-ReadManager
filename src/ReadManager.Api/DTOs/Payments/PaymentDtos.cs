using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Payments;

// Bắt đầu 1 lượt thanh toán cho đơn đang chờ (Pending)
public class StartPaymentDto
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }
}

public class StartPaymentResultDto
{
    public int PaymentId { get; set; }
    public string MerchantRequestId { get; set; } = string.Empty;   // mã yêu cầu gửi sang cổng thanh toán
    public string Provider { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

// CALLBACK do cổng thanh toán gửi về server (server → server, KHÔNG có đăng nhập của người dùng).
// Cổng thật (VNPay/MoMo...) đặt tên trường khác → chỉ cần đổi cách map + cách tính chữ ký.
public class PaymentCallbackDto
{
    [Required, StringLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string EventId { get; set; } = string.Empty;             // ID sự kiện ổn định của cổng → chống xử lý lặp

    [Required, StringLength(100)]
    public string MerchantRequestId { get; set; } = string.Empty;   // mã yêu cầu mình đã gửi lúc bắt đầu thanh toán

    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required, StringLength(150)]
    public string TransactionId { get; set; } = string.Empty;       // mã giao dịch phía cổng

    public decimal Amount { get; set; }

    [Required, StringLength(10)]
    public string Currency { get; set; } = string.Empty;

    [Required, RegularExpression("^(Success|Failed|Cancelled|Pending)$",
        ErrorMessage = "Status phải là Success, Failed, Cancelled hoặc Pending.")]
    public string Status { get; set; } = string.Empty;

    [StringLength(100)]
    public string? FailureCode { get; set; }

    [Required]
    public string Signature { get; set; } = string.Empty;           // HMAC-SHA256 (hex)
}

// Trả lời cổng thanh toán: HTTP 200 = "đã nhận, đừng gửi lại"
public class PaymentCallbackResultDto
{
    public string Result { get; set; } = string.Empty;   // Paid | PaidGrantFailed | Duplicate | Ignored | Failed | Cancelled | Pending | InvalidSignature | PaymentNotFound | Rejected
    public string? Reason { get; set; }                  // mã lý do khi bị từ chối / bỏ qua
    public string Message { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public bool EntitlementGranted { get; set; }
}

// 1 dòng trong danh sách Admin cần xử lý: đã thu tiền (đã xác minh) nhưng chưa có quyền đọc
public class PaymentRecoveryDto
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? ProviderTransactionId { get; set; }
    public DateTime? SucceededAt { get; set; }
    public string? LastError { get; set; }
}

// Kết quả Admin chạy lại cấp quyền
public class RegrantResultDto
{
    public int OrderId { get; set; }
    public bool Granted { get; set; }          // true = đơn này giờ đã có quyền đọc
    public bool AlreadyGranted { get; set; }   // true = trước đó đã cấp rồi → lần chạy lại này KHÔNG cấp thêm
    public string Message { get; set; } = string.Empty;
}

// CHỈ DÙNG KHI THỬ (Development): giả lập cổng thanh toán báo kết quả
public class SimulatePaymentDto
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required, RegularExpression("^(Success|Failed|Cancelled|Pending)$")]
    public string Status { get; set; } = "Success";

    [StringLength(150)]
    public string? TransactionId { get; set; }   // bỏ trống = tự sinh

    [StringLength(150)]
    public string? EventId { get; set; }         // bỏ trống = tự sinh; gửi lại CÙNG mã để thử callback lặp
}
namespace ReadManager.Mobile.Models;

// Khớp DTOs/Payments của Backend 4 (PaymentsController).

// POST api/payments/start — chỉ gửi mã đơn, số tiền do server lấy từ đơn hàng
public class StartPaymentRequest
{
    public int OrderId { get; set; }
}

// Kết quả POST api/payments/start (StartPaymentResultDto)
public class StartPaymentResult
{
    public int PaymentId { get; set; }
    public string MerchantRequestId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

// POST api/payments/dev/simulate — CHỈ khi API chạy ở môi trường Development
public class SimulatePaymentRequest
{
    public int OrderId { get; set; }
    public string Status { get; set; } = "Success";   // Success | Failed | Cancelled | Pending
}

// Kết quả giả lập / callback (PaymentCallbackResultDto)
public class PaymentCallbackResult
{
    public string Result { get; set; } = string.Empty;   // Paid | PaidGrantFailed | Duplicate | Ignored | Failed | Cancelled | Pending | ...
    public string? Reason { get; set; }
    public string Message { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public bool EntitlementGranted { get; set; }
}

// Kết quả hiển thị cho màn đơn hàng
public class PaymentStart
{
    public int? PaymentId { get; set; }
    public string? PayUrl { get; set; }      // cổng thật (VNPay/MoMo...) sau này sẽ trả link; hiện API chưa có -> null
    public string? Status { get; set; }
    public string? Message { get; set; }

    public bool HasPayUrl => !string.IsNullOrWhiteSpace(PayUrl);
}

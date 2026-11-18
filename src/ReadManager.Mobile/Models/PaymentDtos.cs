namespace ReadManager.Mobile.Models;

public class StartPaymentRequest { public int OrderId { get; set; } }
// POST api/payments/start: tạo giao dịch, chưa xác nhận đã thu tiền.
public class PaymentStart
{
    public int PaymentId { get; set; }
    public string MerchantRequestId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
public class PaymentOutcome
{
    public string Result { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string Message { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public bool EntitlementGranted { get; set; }
}

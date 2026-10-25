namespace ReadManager.Api.Entities;

public class Payment
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string MerchantRequestId { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public decimal? ReceivedAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? FailureCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public DateTime? SucceededAt { get; set; }
    public Order Order { get; set; } = null!;
}

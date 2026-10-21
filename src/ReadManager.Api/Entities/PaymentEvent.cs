namespace ReadManager.Api.Entities;

public class PaymentEvent
{
    public int PaymentEventId { get; set; }
    public int PaymentId { get; set; }
    public string ProviderEventId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string ProcessingStatus { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public Payment Payment { get; set; } = null!;
}

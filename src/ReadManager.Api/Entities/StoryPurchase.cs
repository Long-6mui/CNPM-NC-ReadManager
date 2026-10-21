namespace ReadManager.Api.Entities;

public class StoryPurchase
{
    public int PurchaseId { get; set; }
    public int OrderId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Order Order { get; set; } = null!;
}

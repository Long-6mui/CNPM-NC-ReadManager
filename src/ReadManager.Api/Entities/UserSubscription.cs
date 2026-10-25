namespace ReadManager.Api.Entities;

public class UserSubscription
{
    public int SubscriptionId { get; set; }
    public int OrderId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Order Order { get; set; } = null!;
}

namespace ReadManager.Api.DTOs.Entitlements;

public class EntitlementCheckResult
{
    public bool CanRead { get; set; }

    // LoginRequired | SubscriptionExpired | SubscriptionRequired | StoryPurchaseRequired
    public string? Reason { get; set; }

    // Free | Purchased | Subscription | Admin
    public string? AccessType { get; set; }
}

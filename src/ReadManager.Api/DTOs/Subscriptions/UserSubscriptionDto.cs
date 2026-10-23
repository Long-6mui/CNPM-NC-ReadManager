namespace ReadManager.Api.DTOs.Subscriptions;

public class UserSubscriptionDto
{
    public int SubscriptionId { get; set; }
    public int OrderId { get; set; }
    public int? PlanId { get; set; }
    public int? PlanVersionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "Selected"; // All | Selected
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; }
    public int DaysRemaining { get; set; }
}

public class UserSubscriptionSummaryDto
{
    public UserSubscriptionDto? ActiveSubscription { get; set; }
    public List<UserSubscriptionDto> History { get; set; } = new();
}

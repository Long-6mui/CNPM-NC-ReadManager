namespace ReadManager.Mobile.Models;
public class SubscriptionDto
{
    public int SubscriptionId { get; set; }
    public int? PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime? RevokedAt { get; set; }
}
public class SubscriptionSummaryDto
{
    public List<SubscriptionDto> History { get; set; } = new();
}
public class LibraryItemDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
public class UserLibraryDto
{
    public List<LibraryItemDto> PurchasedStories { get; set; } = new();
    public SubscriptionSummaryDto Subscriptions { get; set; } = new();
}

namespace ReadManager.Mobile.Models;

// Khớp DTO thật của Backend 2: GET api/me/library  (DTOs/Library/UserLibraryDto + DTOs/Subscriptions/UserSubscriptionDto)
// (Chỉ dùng trong ApiReadingAccessService.)

public class LibraryResponseDto
{
    public List<PurchasedStoryDto> PurchasedStories { get; set; } = new();
    public SubscriptionSummaryDto Subscriptions { get; set; } = new();
}

public class PurchasedStoryDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public DateTime GrantedAt { get; set; }      // UTC
    public DateTime? ExpiresAt { get; set; }
}

public class SubscriptionSummaryDto
{
    public SubscriptionDto? ActiveSubscription { get; set; }
    public bool HasExpiringAlert { get; set; }
    public string? AlertMessage { get; set; }
    public List<SubscriptionDto> History { get; set; } = new();
}

public class SubscriptionDto
{
    public int SubscriptionId { get; set; }
    public int? PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "Selected";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }         // UTC
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; }
    public int DaysRemaining { get; set; }
}

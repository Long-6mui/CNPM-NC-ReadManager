using ReadManager.Api.DTOs.Subscriptions;

namespace ReadManager.Api.DTOs.Library;

public class PurchasedStoryItemDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class UserLibraryDto
{
    public List<PurchasedStoryItemDto> PurchasedStories { get; set; } = new();
    public UserSubscriptionSummaryDto Subscriptions { get; set; } = new();
}

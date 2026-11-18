namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 (Mở rộng thực tế) — Dòng sự kiện hoạt động gần đây trên hệ thống (Live Activity Feed)
public class RecentActivityEventDto
{
    // NewMember | NewSubscription | Renewal | StoryPurchase | OrderFailed | OrderPending
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Status { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

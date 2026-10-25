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

    // Mở rộng thực tế: Cảnh báo sắp hết hạn (<= 7 ngày) & trạng thái hiển thị
    public bool IsExpiringSoon { get; set; }
    public string StatusBadge { get; set; } = string.Empty; // "Đang hoạt động" | "Sắp hết hạn" | "Đã hết hạn" | "Đã thu hồi"
    public bool CanRenew { get; set; }
}

public class UserSubscriptionSummaryDto
{
    public UserSubscriptionDto? ActiveSubscription { get; set; }
    public bool HasExpiringAlert { get; set; }
    public string? AlertMessage { get; set; }
    public List<UserSubscriptionDto> History { get; set; } = new();
}

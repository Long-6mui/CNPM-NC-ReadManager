namespace ReadManager.Api.DTOs.Subscriptions;

// PB27 — Một kỳ sử dụng gói của thành viên
public class SubscriptionDto
{
    public int SubscriptionId { get; set; }

    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int PlanVersionId { get; set; }
    public int VersionNumber { get; set; }

    public decimal Price { get; set; }             // giá của phiên bản gói lúc mua
    public int DurationMonths { get; set; }
    public string ScopeType { get; set; } = "Selected";   // All | Selected
    public int? StoryCount { get; set; }           // số truyện trong phạm vi (null khi ScopeType = All)

    public DateTime StartAt { get; set; }          // UTC
    public DateTime EndAt { get; set; }            // UTC

    // Active   : đang trong hạn
    // Upcoming : kỳ gia hạn sớm, chưa tới ngày bắt đầu
    // Expired  : đã hết hạn
    // Revoked  : bị Admin thu hồi
    public string Status { get; set; } = "Active";

    public int DaysRemaining { get; set; }         // chỉ > 0 khi Status = Active
}

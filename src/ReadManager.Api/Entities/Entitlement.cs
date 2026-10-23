namespace ReadManager.Api.Entities;

// BE4 — QUYỀN ĐỌC ĐÃ CẤP cho người mua (PB19, PB21, PB25)
// OrderId là DUY NHẤT → 1 đơn chỉ cấp quyền đúng 1 lần; chạy lại cấp quyền không bao giờ cấp trùng.
//   Source = "Story" → quyền đọc riêng 1 truyện (StoryId), ExpiresAt = null (vĩnh viễn, độc lập với gói tháng)
//   Source = "Plan"  → quyền theo gói (PlanVersionId = phiên bản lúc mua), có StartsAt / ExpiresAt
// Phần kiểm tra quyền khi đọc chương (PB22/PB23) sẽ đọc bảng này.
public class Entitlement
{
    public int EntitlementId { get; set; }
    public int UserId { get; set; }
    public int OrderId { get; set; }

    public string Source { get; set; } = string.Empty;   // "Plan" | "Story"
    public int? PlanVersionId { get; set; }
    public int? StoryId { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }             // null = không hết hạn
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Order Order { get; set; } = null!;
    public ReadingPlanVersion? PlanVersion { get; set; }
    public Story? Story { get; set; }
}
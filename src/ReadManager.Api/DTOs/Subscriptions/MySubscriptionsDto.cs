namespace ReadManager.Api.DTOs.Subscriptions;

// PB27 — Gói đang dùng + lịch sử gói của tài khoản
public class MySubscriptionsDto
{
    public bool HasActive { get; set; }

    // Ngày hết hạn xa nhất của chuỗi gói đang dùng / đã gia hạn sớm (UTC). null nếu không có gói hiệu lực.
    public DateTime? ActiveUntil { get; set; }

    // Active + Upcoming, sắp theo ngày bắt đầu
    public List<SubscriptionDto> Current { get; set; } = new();

    // Expired + Revoked, mới nhất trước
    public List<SubscriptionDto> History { get; set; } = new();
}

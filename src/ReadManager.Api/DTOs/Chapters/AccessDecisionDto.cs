namespace ReadManager.Api.DTOs.Library;

// PB22 — KẾT QUẢ KIỂM TRA QUYỀN ĐỌC (Web/Mobile hiện nút "Đọc" hay "Mua/Đăng ký")
public class AccessDecisionDto
{
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }

    public bool HasAccess { get; set; }

    // Vì sao được đọc: Free | Admin | Purchase | Subscription. Không được đọc: None.
    public string Source { get; set; } = "None";

    // Chỉ có khi HasAccess = false:
    //   LoginRequired       – chưa đăng nhập
    //   NotPurchased        – chưa mua truyện / chưa có gói phù hợp
    //   SubscriptionExpired – từng có gói bao phủ truyện này nhưng đã hết hạn (và chưa mua riêng)
    //   NotReleased         – chương chưa tới giờ ra mắt
    public string? Reason { get; set; }

    // Khi Source = Subscription: gói nào, hết hạn khi nào (UTC)
    public string? PlanName { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
}

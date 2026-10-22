namespace ReadManager.Api.DTOs.Subscriptions;

// Kết quả CẤP QUYỀN — Backend 3 (đơn hàng) / Backend 4 (thanh toán, khôi phục quyền) dùng để biết cấp được chưa.
public class GrantResultDto
{
    // true cả khi "đã cấp từ trước" (gọi lặp vẫn an toàn)
    public bool Succeeded { get; set; }

    // Activated | Renewed | Granted | AlreadyGranted | AlreadyOwned
    // PlanVersionNotFound | PlanUnavailable | StoryNotFound | StoryNotPaid | UserNotFound | InvalidAmount
    public string Outcome { get; set; } = string.Empty;

    public int? SubscriptionId { get; set; }   // khi cấp gói
    public int? AccessId { get; set; }         // khi cấp quyền mua truyện
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}

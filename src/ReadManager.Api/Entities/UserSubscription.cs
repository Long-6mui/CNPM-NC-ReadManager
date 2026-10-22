namespace ReadManager.Api.Entities;

// PB19–23, PB27 — MỘT KỲ sử dụng gói đọc của thành viên.
// Mua mới hoặc gia hạn đều sinh thêm 1 dòng → giữ nguyên được lịch sử.
// Hết hạn KHÔNG cần job chạy nền: quyền còn hiệu lực khi StartAt <= bây giờ < EndAt.
public class UserSubscription
{
    public int SubscriptionId { get; set; }
    public int UserId { get; set; }

    // Gắn với PHIÊN BẢN gói tại thời điểm mua (giá, thời hạn, phạm vi truyện đã chốt).
    public int PlanVersionId { get; set; }

    public DateTime StartAt { get; set; }   // UTC
    public DateTime EndAt { get; set; }     // UTC

    // Active | Revoked (Admin thu hồi). "Hết hạn" được tính từ EndAt, không lưu ở đây.
    public string Status { get; set; } = "Active";

    // Đơn hàng sinh ra kỳ này (Backend 3). Dùng làm khóa chống cấp quyền trùng khi callback lặp.
    public int? OrderId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ReadingPlanVersion PlanVersion { get; set; } = null!;
}

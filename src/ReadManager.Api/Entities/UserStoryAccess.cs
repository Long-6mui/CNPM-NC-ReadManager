namespace ReadManager.Api.Entities;

// PB21, PB23, PB27 — QUYỀN ĐỌC RIÊNG một bộ truyện do thành viên mua.
// Độc lập với gói tháng: gói hết hạn thì quyền này vẫn còn (PB23).
// Khóa duy nhất (UserId, StoryId) → không bao giờ cấp trùng.
public class UserStoryAccess
{
    public int AccessId { get; set; }
    public int UserId { get; set; }
    public int StoryId { get; set; }

    // Giá tại thời điểm mua (chốt giá, đổi giá sau này không ảnh hưởng).
    public decimal PricePaid { get; set; }

    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    // Đơn hàng sinh ra quyền này (Backend 3) — chống cấp trùng khi callback lặp.
    public int? OrderId { get; set; }

    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
}

namespace ReadManager.Mobile.Models;

// DỰ ĐOÁN hình dạng JSON của API quyền đọc (Backend 2). SỬA cho khớp DTO thật khi API xong.
// (Chỉ dùng trong ApiReadingAccessService.)

// 1 gói đọc đang có (DTOs/Subscriptions)
public class SubscriptionDto
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

// 1 truyện đã mua trong thư viện (DTOs/Library)
public class LibraryItemDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateTime PurchasedAt { get; set; }
}

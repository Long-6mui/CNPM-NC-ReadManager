using ReadManager.Api.DTOs.Subscriptions;

namespace ReadManager.Api.DTOs.Library;

// PB27 — Tổng hợp quyền lợi của tài khoản: gói đang dùng + truyện đã mua (1 lần gọi cho màn "Thư viện")
public class MyLibraryDto
{
    public MySubscriptionsDto Subscriptions { get; set; } = new();
    public List<PurchasedStoryDto> PurchasedStories { get; set; } = new();
}

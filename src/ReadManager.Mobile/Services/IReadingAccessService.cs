using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// API dùng route/DTO hiện tại. Trạng thái thanh toán và quyền đọc do backend quyết định.
public interface IReadingAccessService
{
    // Gói đang còn hạn + truyện đã mua trọn bộ của người dùng hiện tại
    Task<PurchaseSummary> GetPurchasesAsync();
}

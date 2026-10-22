using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// ============================================================================
// QUYỀN ĐỌC / THƯ VIỆN ĐÃ MUA  —  CHỖ NỐI VỚI BACKEND 2 (feature/s2-reading-access)
//   Backend 2 làm: SubscriptionsController, LibraryController, EntitlementService (PB22–23, PB27).
//
// Giao diện (Tủ sách > Đã mua, màn Gói đọc) chỉ gọi interface này, KHÔNG biết dữ liệu từ đâu ra.
//   - Hiện tại : OrderBasedReadingAccessService  -> tự tính từ các đơn đã thanh toán (api/me/orders)
//   - Khi API xong: bật ApiSettings.UseReadingAccessApi = true  -> ApiReadingAccessService gọi API thật
//                   (nếu API trả 404/405 thì tự quay về cách tính từ đơn hàng, nên bật sớm cũng không hỏng app)
// ============================================================================
public interface IReadingAccessService
{
    // Gói đang còn hạn + truyện đã mua trọn bộ của người dùng hiện tại
    Task<PurchaseSummary> GetPurchasesAsync();
}

using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// ============================================================================
// BẢN NỐI API QUYỀN ĐỌC (Backend 2) — ĐÃ VIẾT SẴN, ĐANG TẮT.
//
// Khi Backend 2 đẩy LibraryController / SubscriptionsController lên GitHub:
//   1) Mở file Models/ReadingAccessDtos.cs, sửa tên trường cho KHỚP DTO thật (DTOs/Library, DTOs/Subscriptions).
//   2) Sửa 2 đường dẫn bên dưới cho đúng route thật.
//   3) Đặt ApiSettings.UseReadingAccessApi = true.
// Đường dẫn và tên trường dưới đây là DỰ ĐOÁN, chưa phải API thật.
// Nếu API trả 404/405 thì tự dùng lại cách tính từ đơn hàng.
// ============================================================================
public class ApiReadingAccessService : IReadingAccessService
{
    // TODO(Backend 2): sửa cho đúng route thật
    private const string SubscriptionsUri = "subscriptions/me";   // gói đang còn hạn của tôi
    private const string LibraryUri = "library";                  // truyện đã mua của tôi

    private readonly ApiClient _api;
    private readonly OrderBasedReadingAccessService _fallback;

    public ApiReadingAccessService(ApiClient api, OrderBasedReadingAccessService fallback)
    {
        _api = api;
        _fallback = fallback;
    }

    public async Task<PurchaseSummary> GetPurchasesAsync()
    {
        try
        {
            var subs = await _api.GetAsync<List<SubscriptionDto>>(SubscriptionsUri) ?? new();
            var library = await _api.GetAsync<List<LibraryItemDto>>(LibraryUri) ?? new();

            return new PurchaseSummary
            {
                Plans = subs
                    .Where(s => s.ExpiresAt > DateTime.UtcNow)
                    .Select(s => new PlanEntitlement
                    {
                        PlanId = s.PlanId,
                        PlanName = s.PlanName,
                        ExpiresAt = Fmt.Local(s.ExpiresAt)
                    })
                    .OrderBy(p => p.ExpiresAt)
                    .ToList(),
                Stories = library
                    .Select(i => new OwnedStory
                    {
                        StoryId = i.StoryId,
                        Title = i.Title,
                        Amount = i.Amount ?? 0,
                        PurchasedAt = Fmt.Local(i.PurchasedAt)
                    })
                    .OrderByDescending(s => s.PurchasedAt)
                    .ToList()
            };
        }
        catch (ApiException ex) when (ex.StatusCode is 404 or 405)
        {
            // API chưa có trên server đang chạy -> dùng cách tính từ đơn hàng
            return await _fallback.GetPurchasesAsync();
        }
    }
}

using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// ============================================================================
// NỐI API QUYỀN ĐỌC THẬT (Backend 2): GET api/me/library
//   -> purchasedStories (truyện đã mua trọn bộ) + subscriptions (gói đang dùng, lịch sử)
// Nếu server đang chạy chưa có API này (404/405) thì tự dùng lại cách tính từ đơn hàng.
// ============================================================================
public class ApiReadingAccessService : IReadingAccessService
{
    private const string LibraryUri = "me/library";

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
            var library = await _api.GetAsync<LibraryResponseDto>(LibraryUri) ?? new LibraryResponseDto();

            var plans = new List<PlanEntitlement>();
            var active = library.Subscriptions.ActiveSubscription;
            if (active is { IsActive: true } && active.RevokedAt is null)
            {
                plans.Add(new PlanEntitlement
                {
                    PlanId = active.PlanId ?? 0,
                    PlanName = active.PlanName,
                    ExpiresAt = Fmt.Local(active.EndsAt)
                });
            }

            return new PurchaseSummary
            {
                Plans = plans,
                Stories = library.PurchasedStories
                    .Select(i => new OwnedStory
                    {
                        StoryId = i.StoryId,
                        Title = i.Title,
                        Amount = 0,   // API thư viện không trả số tiền; số tiền xem ở Lịch sử thanh toán
                        PurchasedAt = Fmt.Local(i.GrantedAt)
                    })
                    .OrderByDescending(s => s.PurchasedAt)
                    .ToList()
            };
        }
        catch (ApiException ex) when (ex.StatusCode is 404 or 405)
        {
            return await _fallback.GetPurchasesAsync();
        }
    }
}

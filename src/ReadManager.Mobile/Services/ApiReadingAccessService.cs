using ReadManager.Mobile.Models;
namespace ReadManager.Mobile.Services;

// Quyền đọc lấy từ server, không suy ra từ trạng thái đơn (quyền có thể đã bị thu hồi).
public class ApiReadingAccessService(ApiClient api) : IReadingAccessService
{
    public async Task<PurchaseSummary> GetPurchasesAsync()
    {
        var library = await api.GetAsync<UserLibraryDto>("me/library")
            ?? throw new ApiException(500, "API trả thư viện trống.");
        return new PurchaseSummary
        {
            Plans = library.Subscriptions.History.Where(s => s.IsActive && s.RevokedAt is null)
                .Select(s => new PlanEntitlement { PlanId = s.PlanId ?? 0, PlanName = s.PlanName, ExpiresAt = Fmt.Local(s.EndsAt) })
                .OrderBy(p => p.ExpiresAt).ToList(),
            Stories = library.PurchasedStories.Select(i => new OwnedStory
            {
                StoryId = i.StoryId, Title = i.Title, Amount = null, PurchasedAt = Fmt.Local(i.GrantedAt)
            }).OrderByDescending(s => s.PurchasedAt).ToList()
        };
    }
}

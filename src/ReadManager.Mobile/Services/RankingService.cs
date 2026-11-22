using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Bảng xếp hạng truyện ở Trang chủ — gọi API có sẵn của Backend 3:
//   GET api/ranking/stories?by=reads|follows|score&period=day|week|month|all&genreId=&top=
public class RankingService
{
    private readonly ApiClient _api;

    public RankingService(ApiClient api) => _api = api;

    public async Task<List<StorySummary>> GetStoriesAsync(string by, string period, int? genreId, int top)
    {
        var url = $"ranking/stories?by={Uri.EscapeDataString(by)}&period={Uri.EscapeDataString(period)}&top={top}";
        if (genreId.HasValue) url += $"&genreId={genreId.Value}";
        return await _api.GetAsync<List<StorySummary>>(url) ?? new List<StorySummary>();
    }
}

using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

public class StreakService : IStreakService
{
    private readonly ApiClient _api;

    public StreakService(ApiClient api) => _api = api;

    public async Task<StreakInfo> GetAsync()
        => await _api.GetAsync<StreakInfo>("me/streak") ?? new StreakInfo();

    public Task BuyCardAsync(Guid requestId) => _api.PostAsync($"reader/freeze-cards/{requestId}", new { });

    public async Task<StreakInfo> RestoreAsync()
        => await _api.PostAsync<object, StreakInfo>("me/streak/restore", new { })
           ?? throw new ApiException(500, "Không nhận được phản hồi từ máy chủ.");

    public async Task<StreakInfo> EquipAsync(string? title, string? frame)
        => await _api.PutAsync<EquipRequest, StreakInfo>("me/streak/appearance", new EquipRequest { Title = title, Frame = frame })
           ?? throw new ApiException(500, "Không nhận được phản hồi từ máy chủ.");

    public async Task<List<StreakCoupon>> GetCouponsAsync()
        => await _api.GetAsync<List<StreakCoupon>>("me/coupons") ?? new();

    public async Task<List<StreakNotice>> GetNoticesAsync(int take = 30)
        => await _api.GetAsync<List<StreakNotice>>($"me/streak/notices?take={take}") ?? new();

    public async Task MarkAllNoticesReadAsync()
        => await _api.PutAsync<object, object>("me/streak/notices/read-all", new { });

    public async Task<List<StreakRankItem>> GetLeaderboardAsync(int top = 20)
        => await _api.GetAsync<List<StreakRankItem>>($"ranking/streak?top={top}") ?? new();
}

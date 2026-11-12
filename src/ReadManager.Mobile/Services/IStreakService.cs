using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Chuỗi đọc sách (Sprint 5) — khớp StreakController của API
public interface IStreakService
{
    Task<StreakInfo> GetAsync();
    Task<StreakInfo> RestoreAsync();
    Task<StreakInfo> EquipAsync(string? title, string? frame);
    Task<List<StreakCoupon>> GetCouponsAsync();
    Task<List<StreakNotice>> GetNoticesAsync(int take = 30);
    Task MarkAllNoticesReadAsync();
    Task<List<StreakRankItem>> GetLeaderboardAsync(int top = 20);
}

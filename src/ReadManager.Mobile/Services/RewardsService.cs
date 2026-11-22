using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Trang "Khám phá": điểm danh, nhiệm vụ hằng ngày, shop đổi voucher
//   GET  api/me/daily-rewards, POST api/me/daily-rewards/check-in, POST api/me/daily-rewards/missions/{key}/claim
//   GET  api/vouchers/offers,  POST api/vouchers/offers/{id}/redeem, GET api/me/vouchers
public class RewardsService
{
    private readonly ApiClient _api;

    // Số xu thay đổi (điểm danh, nhận nhiệm vụ, đổi voucher, đọc xong chương) -> header/trang khác cập nhật
    public static event EventHandler? CoinsChanged;
    public static void NotifyCoinsChanged() => CoinsChanged?.Invoke(null, EventArgs.Empty);

    public RewardsService(ApiClient api) => _api = api;

    public Task<DailyRewards?> GetDailyAsync() => _api.GetAsync<DailyRewards>("me/daily-rewards");

    public Task<RewardClaimResult?> CheckInAsync()
        => _api.PostAsync<object, RewardClaimResult>("me/daily-rewards/check-in", new { });

    public Task<RewardClaimResult?> ClaimMissionAsync(string key)
        => _api.PostAsync<object, RewardClaimResult>($"me/daily-rewards/missions/{Uri.EscapeDataString(key)}/claim", new { });

    public async Task<List<VoucherOffer>> GetOffersAsync()
        => await _api.GetAsync<List<VoucherOffer>>("vouchers/offers") ?? new List<VoucherOffer>();

    public async Task<List<MyVoucher>> GetMyVouchersAsync()
        => await _api.GetAsync<List<MyVoucher>>("me/vouchers") ?? new List<MyVoucher>();

    // requestId mới cho mỗi lần bấm "Đổi" (API dùng để chống trừ xu hai lần khi gửi lại)
    public Task<MyVoucher?> RedeemAsync(int offerId, Guid requestId)
        => _api.PostAsync<object, MyVoucher>($"vouchers/offers/{offerId}/redeem", new { requestId });
}

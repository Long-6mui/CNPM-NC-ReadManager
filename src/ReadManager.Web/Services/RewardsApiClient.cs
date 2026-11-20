using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReadManager.Web.Services;

// ----- Chuỗi đọc + coupon gói (đã có trên API: StreakController) -----
public record StreakDayApi(string Date, string Label, bool Read, bool IsToday);
public record StreakRewardApi(string Kind, string Code, string Name, bool Unlocked);
public record StreakMilestoneApi(int Days, string Name, bool Reached, List<StreakRewardApi> Rewards);
public record StreakApi(
    int CurrentStreak, int LongestStreak, bool ReadToday, int FreezeCards,
    int RestoresUsed, int RestoresLeft, bool CanRestore, int BrokenStreak, DateTime? RestoreDeadline,
    string IconTier, string? SelectedTitle, string? SelectedFrame,
    int NextMilestoneDays, string? NextMilestoneName, int UnreadNotices,
    List<StreakDayApi> Last7Days, List<StreakMilestoneApi> Milestones, List<StreakRewardApi> Collection);
public record CouponApi(int CouponId, string Name, int Percent, List<int> DurationMonths, DateTime ExpiresAt);

// ----- Lịch sử cộng/trừ xu — HỢP ĐỒNG API cho Backend (xem doc/WEB_REWARDS_API_CONTRACT.md) -----
//   GET api/reader/coin-history?page=1&pageSize=20  → PagedApi<CoinTransactionApi>
//   Amount > 0 là cộng xu, < 0 là trừ xu. Type: ChapterReward | StreakReward | FreezeCard | Voucher | AdminGrant | Refund
public record CoinTransactionApi(long Id, int Amount, string Type, string Description, int? BalanceAfter, DateTime CreatedAt);

// ----- Voucher mua truyện — HỢP ĐỒNG API cho Backend (Voucher) -----
//   GET  api/vouchers/offers                         → List<VoucherOfferApi>  (voucher đang cho đổi bằng xu)
//   POST api/vouchers/offers/{offerId}/redeem        body { requestId }  → VoucherApi (đổi lại cùng requestId không trừ xu lần 2)
//   GET  api/me/vouchers?status=Available            → List<VoucherApi>  (voucher của tài khoản)
// DiscountType: "Percent" (Value = %) hoặc "Amount" (Value = số tiền VND). Status: Available | Reserved | Used | Expired
public record VoucherOfferApi(int OfferId, string Name, string? Description, int CoinCost, string DiscountType, decimal Value, decimal? MaxDiscount, decimal? MinOrderAmount, int ValidDays);
public record VoucherApi(int VoucherId, string Name, string DiscountType, decimal Value, decimal? MaxDiscount, decimal? MinOrderAmount, string Status, DateTime ExpiresAt, DateTime? UsedAt);

public record RewardsReply<T>(bool Ok, string Message, T? Data, bool NotAvailable = false);

public class RewardsApiClient(HttpClient http)
{
    public Task<RewardsReply<StreakApi>> StreakAsync(string? token) => Send<StreakApi>(HttpMethod.Get, "api/me/streak", token);
    public Task<RewardsReply<StreakApi>> RestoreStreakAsync(string? token) => Send<StreakApi>(HttpMethod.Post, "api/me/streak/restore", token, new { });
    public Task<RewardsReply<List<CouponApi>>> CouponsAsync(string? token) => Send<List<CouponApi>>(HttpMethod.Get, "api/me/coupons", token);

    public Task<RewardsReply<PagedApi<CoinTransactionApi>>> CoinHistoryAsync(int page, string? token)
        => Send<PagedApi<CoinTransactionApi>>(HttpMethod.Get, $"api/reader/coin-history?page={Math.Max(1, page)}&pageSize=20", token);

    public Task<RewardsReply<List<VoucherOfferApi>>> VoucherOffersAsync(string? token) => Send<List<VoucherOfferApi>>(HttpMethod.Get, "api/vouchers/offers", token);
    public Task<RewardsReply<List<VoucherApi>>> MyVouchersAsync(string? status, string? token)
        => Send<List<VoucherApi>>(HttpMethod.Get, "api/me/vouchers" + (string.IsNullOrEmpty(status) ? "" : $"?status={Uri.EscapeDataString(status)}"), token);
    public Task<RewardsReply<VoucherApi>> RedeemVoucherAsync(int offerId, Guid requestId, string? token)
        => Send<VoucherApi>(HttpMethod.Post, $"api/vouchers/offers/{offerId}/redeem", token, new { requestId });

    private async Task<RewardsReply<T>> Send<T>(HttpMethod method, string path, string? token, object? body = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return new(true, "", default);
                var data = await response.Content.ReadFromJsonAsync<T>();
                return new(data is not null, data is null ? "API trả dữ liệu trống." : "", data);
            }
            string? message = null;
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) message = m.GetString();
            }
            catch (JsonException) { }
            var code = (int)response.StatusCode;
            return new(false, message ?? code switch
            {
                401 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                403 => "Bạn không có quyền thực hiện thao tác này.",
                404 => "Chức năng này chưa được bật trên API.",
                _ => $"API trả lỗi {code}. Vui lòng thử lại."
            }, default, NotAvailable: code is 404 or 405);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.", default);
        }
    }
}

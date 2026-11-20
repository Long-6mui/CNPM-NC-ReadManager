using ReadManager.Web.Services;

namespace ReadManager.Web.ViewModels;

// Trang "Ví xu & chuỗi đọc": số dư xu, chuỗi, thẻ bảo vệ, coupon gói, voucher mua truyện, lịch sử xu
public class RewardsVm
{
    public long? Coins { get; set; }
    public int FreezeCards { get; set; }
    public string? WalletError { get; set; }

    public StreakApi? Streak { get; set; }
    public string? StreakError { get; set; }
    public List<CouponApi> Coupons { get; set; } = [];

    public List<VoucherOfferApi> Offers { get; set; } = [];
    public List<VoucherApi> Vouchers { get; set; } = [];
    public bool VouchersUnavailable { get; set; }
    public string? VoucherError { get; set; }

    public PagedApi<CoinTransactionApi>? History { get; set; }
    public bool HistoryUnavailable { get; set; }
    public string? HistoryError { get; set; }
}

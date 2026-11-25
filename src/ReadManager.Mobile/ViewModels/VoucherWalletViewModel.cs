using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// "KHO MÃ GIẢM GIÁ" của tài khoản đang đăng nhập
//  - Tab "Voucher mua truyện": voucher đổi bằng xu (GET api/me/vouchers), lọc Có thể dùng / Đã dùng / Hết hạn.
//    Dùng khi MUA TRỌN BỘ TRUYỆN (chọn ở màn Thanh toán).
//  - Tab "Mã gói đọc": mã giảm % nhận từ mốc CHUỖI ĐỌC (GET api/me/coupons). Dùng khi MUA / GIA HẠN GÓI ĐỌC.
public partial class VoucherWalletViewModel : BaseViewModel
{
    private const string VoucherTab = "voucher";
    private const string CouponTab = "coupon";

    private readonly RewardsService _rewards;
    private readonly IStreakService _streak;
    private List<MyVoucher> _vouchers = new();

    public ObservableCollection<ChipItem> Tabs { get; } = new()
    {
        new ChipItem(VoucherTab, "Voucher mua truyện") { IsSelected = true },
        new ChipItem(CouponTab, "Mã gói đọc (chuỗi)"),
    };

    public ObservableCollection<ChipItem> Filters { get; } = new()
    {
        new ChipItem("usable", "Có thể dùng") { IsSelected = true },
        new ChipItem("used", "Đã dùng"),
        new ChipItem("expired", "Hết hạn"),
    };

    public ObservableCollection<MyVoucher> Vouchers { get; } = new();
    public ObservableCollection<StreakCoupon> Coupons { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVoucherTab))]
    [NotifyPropertyChangedFor(nameof(IsCouponTab))]
    private string currentTab = VoucherTab;

    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private bool showVoucherEmpty;
    [ObservableProperty] private bool showCouponEmpty;
    [ObservableProperty] private string voucherEmptyText = string.Empty;
    [ObservableProperty] private string summary = string.Empty;

    public bool IsVoucherTab => CurrentTab == VoucherTab;
    public bool IsCouponTab => CurrentTab == CouponTab;

    public VoucherWalletViewModel(RewardsService rewards, IStreakService streak)
    {
        _rewards = rewards;
        _streak = streak;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            _vouchers = await _rewards.GetMyVouchersAsync();
            Coupons.Clear();
            foreach (var c in await _streak.GetCouponsAsync()) Coupons.Add(c);
            ShowCouponEmpty = Coupons.Count == 0;

            var usable = _vouchers.Count(v => v.Group == "usable");
            Summary = $"{usable} voucher mua truyện · {Coupons.Count} mã gói đọc đang dùng được";
            for (var i = 0; i < Filters.Count; i++)
            {
                var key = Filters[i].Key;
                Filters[i].Badge = _vouchers.Count(v => v.Group == key).ToString();
            }
            ApplyFilter();
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private void SelectTab(ChipItem? tab)
    {
        if (tab is null || tab.IsSelected) return;
        foreach (var t in Tabs) t.IsSelected = ReferenceEquals(t, tab);
        CurrentTab = tab.Key;
    }

    [RelayCommand]
    private void SelectFilter(ChipItem? filter)
    {
        if (filter is null || filter.IsSelected) return;
        foreach (var f in Filters) f.IsSelected = ReferenceEquals(f, filter);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var key = Filters.FirstOrDefault(f => f.IsSelected)?.Key ?? "usable";
        Vouchers.Clear();
        foreach (var v in _vouchers.Where(v => v.Group == key).OrderBy(v => v.ExpiresAt)) Vouchers.Add(v);
        ShowVoucherEmpty = Vouchers.Count == 0;
        VoucherEmptyText = key switch
        {
            "used" => "Chưa dùng voucher nào.",
            "expired" => "Không có voucher hết hạn.",
            _ => "Kho chưa có voucher. Đổi xu lấy voucher ở Khám phá → Shop đổi voucher."
        };
    }

    // Voucher mua truyện: đi tìm truyện trả phí để mua (chọn voucher ở màn Thanh toán)
    [RelayCommand]
    private async Task UseVoucherAsync() => await Shell.Current.GoToAsync(nameof(Views.SearchPage));

    // Mã gói đọc: mở danh sách gói để mua / gia hạn
    [RelayCommand]
    private async Task UseCouponAsync() => await Shell.Current.GoToAsync(nameof(Views.PlansPage));

    [RelayCommand]
    private async Task OpenShopAsync() => await Shell.Current.GoToAsync("//BrowsePage");

    [RelayCommand]
    private async Task OpenStreakAsync() => await Shell.Current.GoToAsync(nameof(Views.StreakPage));
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "KHÁM PHÁ" (tham khảo ảnh mẫu):
//  1. Điểm danh nhận thưởng: 7 ô (T2 → CN), bấm ô "Hôm nay" để nhận xu
//  2. Chuỗi đọc sách 7 ngày + nút "Xem chi tiết chuỗi"
//  3. Hai mục: "Nhiệm vụ hằng ngày" (nhận xu khi hoàn thành) | "Shop đổi voucher" (đổi xu lấy voucher mua truyện)
public partial class ExploreViewModel : BaseViewModel
{
    private const string MissionsTab = "missions";
    private const string ShopTab = "shop";

    private readonly IStreakService _streak;
    private readonly ReaderActivityService _reader;
    private readonly IAuthService _auth;
    private readonly RewardsService _rewards;

    public ObservableCollection<StreakDay> Days { get; } = new();          // chuỗi 7 ngày
    public ObservableCollection<CheckInDay> CheckInDays { get; } = new();  // điểm danh T2..CN
    public ObservableCollection<DailyMission> Missions { get; } = new();
    public ObservableCollection<VoucherOffer> Offers { get; } = new();
    public ObservableCollection<MyVoucher> MyVouchers { get; } = new();
    public ObservableCollection<CoinTransaction> History { get; } = new();

    public ObservableCollection<ChipItem> Tabs { get; } = new()
    {
        new ChipItem(MissionsTab, "Nhiệm vụ hằng ngày") { IsSelected = true },
        new ChipItem(ShopTab, "Shop đổi voucher"),
    };

    // Trang hiện hiệu ứng "+10 xu" khi nhận thưởng thành công
    public event EventHandler<string>? Celebrate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    private bool isLoggedIn;

    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private StreakInfo info = new();
    [ObservableProperty] private double milestoneProgress;
    [ObservableProperty] private string coinsText = "0";
    [ObservableProperty] private string freezeText = string.Empty;
    [ObservableProperty] private bool hasHistory;
    [ObservableProperty] private bool hasMyVouchers;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoOffers))]
    private bool hasOffers;
    public bool HasNoOffers => !HasOffers;
    [ObservableProperty] private string weekText = string.Empty;
    [ObservableProperty] private string shopMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CheckInText))]
    [NotifyCanExecuteChangedFor(nameof(CheckInCommand))]
    private bool checkedInToday;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissionsTab))]
    [NotifyPropertyChangedFor(nameof(IsShopTab))]
    private string currentTab = MissionsTab;

    private int _todayAmount;

    public bool IsGuest => !IsLoggedIn;
    public bool IsMissionsTab => CurrentTab == MissionsTab;
    public bool IsShopTab => CurrentTab == ShopTab;
    public string CheckInText => CheckedInToday ? "✓ Hôm nay đã điểm danh" : $"Điểm danh hôm nay  +{_todayAmount} xu";

    public ExploreViewModel(IStreakService streak, ReaderActivityService reader, IAuthService auth, RewardsService rewards)
    {
        _streak = streak;
        _reader = reader;
        _auth = auth;
        _rewards = rewards;
    }

    // ===================== TẢI DỮ LIỆU =====================
    [RelayCommand]
    private async Task LoadAsync()
    {
        try { IsLoggedIn = await _auth.IsLoggedInAsync(); }
        catch { IsLoggedIn = _auth.CurrentUser is not null; }

        // Shop voucher xem được cả khi chưa đăng nhập
        await LoadOffersAsync();
        if (!IsLoggedIn) return;

        await RunSafeAsync(async () =>
        {
            await LoadDailyAsync();

            Info = await _streak.GetAsync();
            Days.Clear();
            foreach (var d in Info.Last7Days) Days.Add(d);
            MilestoneProgress = Info.NextMilestoneDays > 0
                ? Math.Clamp(Info.CurrentStreak / (double)Info.NextMilestoneDays, 0, 1)
                : 1;
            FreezeText = $"🧊 {Info.FreezeCards} thẻ bảo vệ";
        });

        await LoadMyVouchersAsync();
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
        IsRefreshing = false;
    }

    private async Task LoadDailyAsync()
    {
        var daily = await _rewards.GetDailyAsync();
        if (daily is null) return;

        CheckInDays.Clear();
        foreach (var d in daily.Days) CheckInDays.Add(d);
        _todayAmount = daily.Days.FirstOrDefault(d => d.IsToday)?.Amount ?? 10;
        CheckedInToday = daily.CheckedInToday;
        OnPropertyChanged(nameof(CheckInText));
        WeekText = $"Đã điểm danh {daily.WeekClaimed}/7";
        CoinsText = $"{daily.Coins:N0}";

        Missions.Clear();
        foreach (var m in daily.Missions) Missions.Add(m);
    }

    private async Task LoadOffersAsync()
    {
        try
        {
            var offers = await _rewards.GetOffersAsync();
            Offers.Clear();
            foreach (var o in offers) Offers.Add(o);
            HasOffers = Offers.Count > 0;
        }
        catch { HasOffers = false; }
    }

    private async Task LoadMyVouchersAsync()
    {
        try
        {
            var mine = await _rewards.GetMyVouchersAsync();
            MyVouchers.Clear();
            foreach (var v in mine.OrderBy(v => v.Status == "Available" ? 0 : 1).ThenByDescending(v => v.CreatedAt))
                MyVouchers.Add(v);
            HasMyVouchers = MyVouchers.Count > 0;
        }
        catch { HasMyVouchers = false; }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var page = await _reader.CoinHistory(8);
            History.Clear();
            foreach (var t in page?.Items ?? new List<CoinTransaction>()) History.Add(t);
            HasHistory = History.Count > 0;
        }
        catch { HasHistory = false; }
    }

    // ===================== ĐIỂM DANH =====================
    private bool CanCheckIn() => !CheckedInToday;

    [RelayCommand(CanExecute = nameof(CanCheckIn))]
    private async Task CheckInAsync()
    {
        if (!IsLoggedIn) { await GoToLoginAsync(); return; }
        try
        {
            var result = await _rewards.CheckInAsync();
            if (result is not null) Celebrate?.Invoke(this, $"🎉 Điểm danh thành công  +{result.Amount} xu");
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            Celebrate?.Invoke(this, "Hôm nay bạn đã điểm danh rồi");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Chưa điểm danh được", ex.Message, "OK");
            return;
        }
        RewardsService.NotifyCoinsChanged();
        await RunSafeAsync(LoadDailyAsync);
        await LoadHistoryAsync();
    }

    // ===================== NHIỆM VỤ =====================
    [RelayCommand]
    private async Task ClaimMissionAsync(DailyMission? mission)
    {
        if (mission is null || mission.Claimed) return;
        if (!mission.Completed)
        {
            await Shell.Current.DisplayAlert(mission.Title, $"{mission.Description}.\nTiến độ hôm nay: {mission.ProgressText}", "OK");
            return;
        }
        try
        {
            var result = await _rewards.ClaimMissionAsync(mission.Key);
            mission.Claimed = true;
            if (result is not null) Celebrate?.Invoke(this, $"🎉 Hoàn thành nhiệm vụ  +{result.Amount} xu");
            if (result is not null) CoinsText = $"{result.Coins:N0}";
        }
        catch (ApiException ex) when (ex.StatusCode == 409) { mission.Claimed = true; }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Chưa nhận được thưởng", ex.Message, "OK");
            return;
        }
        RewardsService.NotifyCoinsChanged();
        await LoadHistoryAsync();
    }

    // ===================== SHOP VOUCHER =====================
    [RelayCommand]
    private async Task RedeemAsync(VoucherOffer? offer)
    {
        if (offer is null) return;
        if (!IsLoggedIn) { await GoToLoginAsync(); return; }

        var ok = await Shell.Current.DisplayAlert("Đổi voucher",
            $"Dùng {offer.CostText} để đổi “{offer.Name}”?\n{offer.DiscountText} · {offer.ConditionText}", "Đổi", "Hủy");
        if (!ok) return;

        try
        {
            var voucher = await _rewards.RedeemAsync(offer.OfferId, Guid.NewGuid());
            ShopMessage = string.Empty;
            Celebrate?.Invoke(this, $"🎟 Đã đổi “{voucher?.Name ?? offer.Name}”");
        }
        catch (Exception ex)
        {
            // Thiếu xu / voucher ngừng áp dụng... (API trả lời bằng tiếng Việt)
            ShopMessage = ex.Message;
            await Shell.Current.DisplayAlert("Chưa đổi được", ex.Message, "OK");
            return;
        }
        RewardsService.NotifyCoinsChanged();
        await RunSafeAsync(LoadDailyAsync);   // cập nhật số xu
        await LoadMyVouchersAsync();
        await LoadHistoryAsync();
    }

    // ===================== KHÁC =====================
    [RelayCommand]
    private void SelectTab(ChipItem? tab)
    {
        if (tab is null || tab.IsSelected) return;
        foreach (var t in Tabs) t.IsSelected = ReferenceEquals(t, tab);
        CurrentTab = tab.Key;
    }

    [RelayCommand]
    private async Task OpenWalletAsync() => await Shell.Current.GoToAsync(nameof(Views.VoucherWalletPage));

    [RelayCommand]
    private async Task GoToStreakAsync() => await Shell.Current.GoToAsync(nameof(Views.StreakPage));

    [RelayCommand]
    private async Task GoToSearchAsync() => await Shell.Current.GoToAsync(nameof(Views.SearchPage));

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));
}

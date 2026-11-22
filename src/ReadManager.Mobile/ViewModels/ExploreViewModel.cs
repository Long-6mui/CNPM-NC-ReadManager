using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "KHÁM PHÁ" (làm lại): chuỗi đọc theo từng ngày (giống trang Chuỗi) + nút xem chi tiết chuỗi,
// bên dưới là số xu, thẻ bảo vệ chuỗi và lịch sử xu gần đây.
// (Tìm truyện / lọc thể loại đã chuyển sang trang Tìm kiếm — nút 🔍 trên header.)
public partial class ExploreViewModel : BaseViewModel
{
    private readonly IStreakService _streak;
    private readonly ReaderActivityService _reader;
    private readonly IAuthService _auth;

    public ObservableCollection<StreakDay> Days { get; } = new();
    public ObservableCollection<CoinTransaction> History { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    private bool isLoggedIn;

    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private StreakInfo info = new();
    [ObservableProperty] private double milestoneProgress;
    [ObservableProperty] private string coinsText = "0";
    [ObservableProperty] private string freezeText = string.Empty;
    [ObservableProperty] private bool hasHistory;

    public bool IsGuest => !IsLoggedIn;

    public ExploreViewModel(IStreakService streak, ReaderActivityService reader, IAuthService auth)
    {
        _streak = streak;
        _reader = reader;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try { IsLoggedIn = await _auth.IsLoggedInAsync(); }
        catch { IsLoggedIn = _auth.CurrentUser is not null; }
        if (!IsLoggedIn) return;

        await RunSafeAsync(async () =>
        {
            // Chuỗi đọc 7 ngày gần nhất
            Info = await _streak.GetAsync();
            Days.Clear();
            foreach (var d in Info.Last7Days) Days.Add(d);
            MilestoneProgress = Info.NextMilestoneDays > 0
                ? Math.Clamp(Info.CurrentStreak / (double)Info.NextMilestoneDays, 0, 1)
                : 1;

            // Xu + thẻ bảo vệ
            var profile = await _reader.Profile();
            CoinsText = $"{(profile?.Coins ?? 0):N0}";
            FreezeText = $"🧊 {Info.FreezeCards} thẻ bảo vệ chuỗi";

            // Lịch sử xu gần đây
            History.Clear();
            try
            {
                var page = await _reader.CoinHistory(8);
                foreach (var t in page?.Items ?? new List<CoinTransaction>()) History.Add(t);
            }
            catch { /* lịch sử lỗi không ảnh hưởng phần còn lại */ }
            HasHistory = History.Count > 0;
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
    private async Task GoToStreakAsync() => await Shell.Current.GoToAsync(nameof(Views.StreakPage));

    [RelayCommand]
    private async Task GoToSearchAsync() => await Shell.Current.GoToAsync(nameof(Views.SearchPage));

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));
}

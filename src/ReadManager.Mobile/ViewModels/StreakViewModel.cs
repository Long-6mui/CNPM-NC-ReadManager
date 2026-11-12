using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Màn "Chuỗi đọc sách" (Sprint 5): chuỗi hiện tại, 7 ngày gần nhất, thẻ bảo vệ, khôi phục, mốc thưởng,
// bộ sưu tập (đeo danh hiệu / khung avatar), thông báo về chuỗi, bảng xếp hạng.
public partial class StreakViewModel : BaseViewModel
{
    private readonly IStreakService _streak;
    private readonly IAuthService _auth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    private bool isLoggedIn;

    [ObservableProperty] private StreakInfo info = new();
    [ObservableProperty] private bool hasNotices;
    [ObservableProperty] private bool hasBoard;
    [ObservableProperty] private string message = string.Empty;

    public bool IsGuest => !IsLoggedIn;

    public ObservableCollection<StreakNotice> Notices { get; } = new();
    public ObservableCollection<StreakRankItem> Board { get; } = new();
    public ObservableCollection<StreakRewardItem> Collection { get; } = new();

    public StreakViewModel(IStreakService streak, IAuthService auth)
    {
        _streak = streak;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            IsLoggedIn = await _auth.IsLoggedInAsync();
            if (!IsLoggedIn) return;
            await ReloadAsync();
        });
    }

    private async Task ReloadAsync()
    {
        Info = await _streak.GetAsync();

        Collection.Clear();
        foreach (var c in Info.Collection) Collection.Add(c);

        Notices.Clear();
        foreach (var n in await _streak.GetNoticesAsync(20)) Notices.Add(n);
        HasNotices = Notices.Count > 0;

        Board.Clear();
        try { foreach (var b in await _streak.GetLeaderboardAsync(10)) Board.Add(b); } catch { /* bảng xếp hạng lỗi không ảnh hưởng */ }
        HasBoard = Board.Count > 0;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private Guid _purchaseId = Guid.NewGuid();

    [RelayCommand]
    private async Task BuyCardAsync()
    {
        if (!await Shell.Current.DisplayAlert("Đổi thẻ bảo vệ chuỗi", "Dùng 50 xu để nhận 1 thẻ?", "Đổi thẻ", "Để sau")) return;
        await RunSafeAsync(async () =>
        {
            await _streak.BuyCardAsync(_purchaseId);
            _purchaseId = Guid.NewGuid();
            Message = "Đã đổi 50 xu lấy một thẻ bảo vệ chuỗi.";
            await ReloadAsync();
        });
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var ok = await Shell.Current.DisplayAlert("Khôi phục chuỗi",
            $"Khôi phục lại chuỗi {Info.BrokenStreak} ngày? Bạn còn {Info.RestoresLeft}/5 lượt khôi phục (dùng xong 1 lượt sẽ trừ đi).",
            "Khôi phục", "Để sau");
        if (!ok) return;

        await RunSafeAsync(async () =>
        {
            Info = await _streak.RestoreAsync();
            Message = $"Đã khôi phục! Chuỗi hiện tại: {Info.CurrentStreak} ngày.";
            await ReloadAsync();
        });
    }

    // Đeo danh hiệu hoặc khung avatar đã mở khóa
    [RelayCommand]
    private async Task EquipAsync(StreakRewardItem? item)
    {
        if (item is null || !item.CanEquip) return;
        await RunSafeAsync(async () =>
        {
            Info = item.IsTitle
                ? await _streak.EquipAsync(item.Name, Info.SelectedFrame)
                : await _streak.EquipAsync(Info.SelectedTitle, item.Code);
            Message = $"Đã đeo: {item.Name}";
        });
    }

    [RelayCommand]
    private async Task UnequipTitleAsync()
        => await RunSafeAsync(async () => Info = await _streak.EquipAsync(null, Info.SelectedFrame));

    [RelayCommand]
    private async Task UnequipFrameAsync()
        => await RunSafeAsync(async () => Info = await _streak.EquipAsync(Info.SelectedTitle, null));

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await RunSafeAsync(async () =>
        {
            await _streak.MarkAllNoticesReadAsync();
            await ReloadAsync();
        });
    }

    [RelayCommand]
    private async Task GoToPlansAsync() => await Shell.Current.GoToAsync(nameof(Views.PlansPage));

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToBrowseAsync() => await Shell.Current.GoToAsync("//BrowsePage");
}

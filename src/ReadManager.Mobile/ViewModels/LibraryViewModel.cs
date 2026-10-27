using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "Tủ sách": 4 mục — "Đang đọc" (Sprint 3), "Đã xem gần đây" (lịch sử), "Yêu thích" và "Đã mua" (gói đang dùng + truyện đã mua trọn bộ).
public partial class LibraryViewModel : BaseViewModel
{
    private readonly ILibraryService _library;
    private readonly IAuthService _auth;
    private readonly IReadingAccessService _access;
    private readonly IReadingProgressService _progress;

    public ObservableCollection<LibraryEntry> Items { get; } = new();

    // ----- Mục "Đã mua" (Sprint 2) -----
    public ObservableCollection<PlanEntitlement> MyPlans { get; } = new();
    public ObservableCollection<OwnedStory> OwnedStories { get; } = new();

    [ObservableProperty] private bool purchasesNeedLogin;
    [ObservableProperty] private bool purchasesEmpty;
    [ObservableProperty] private bool hasMyPlans;
    [ObservableProperty] private bool hasOwnedStories;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTab0))]
    [NotifyPropertyChangedFor(nameof(IsTab1))]
    [NotifyPropertyChangedFor(nameof(IsTab2))]
    [NotifyPropertyChangedFor(nameof(IsTab3))]
    [NotifyPropertyChangedFor(nameof(ShowProgressInfo))]
    [NotifyPropertyChangedFor(nameof(ShowList))]
    [NotifyPropertyChangedFor(nameof(ShowClear))]
    private int selectedTab = 3;   // mở tủ sách là thấy ngay "Đang đọc"

    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private string emptyTitle = string.Empty;
    [ObservableProperty] private string emptyHint = string.Empty;
    [ObservableProperty] private string countText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClear))]
    private bool hasItems;

    public bool IsTab0 => SelectedTab == 0;
    public bool IsTab1 => SelectedTab == 1;
    public bool IsTab2 => SelectedTab == 2;
    public bool IsTab3 => SelectedTab == 3;                    // "Đang đọc"
    public bool ShowProgressInfo => IsTab0 || IsTab3;          // hiện "đang đọc chương nào + lúc xem"
    public bool ShowList => SelectedTab != 2;
    public bool ShowClear => IsTab0 && HasItems;

    public LibraryViewModel(ILibraryService library, IAuthService auth, IReadingAccessService access,
        IReadingProgressService progress)
    {
        _progress = progress;
        _library = library;
        _auth = auth;
        _access = access;
    }

    // Gọi mỗi lần tab hiện ra để cập nhật sau khi xem truyện / bấm yêu thích
    [RelayCommand]
    private async Task LoadAsync()
    {
        try { await _auth.IsLoggedInAsync(); } // nạp CurrentUser để chọn đúng tủ sách của tài khoản
        catch { /* mất mạng: dùng tủ sách theo thông tin hiện có */ }
        await ShowAsync();
    }

    [RelayCommand]
    private async Task SelectTabAsync(string? index)
    {
        SelectedTab = int.TryParse(index, out var i) ? i : 0;
        await ShowAsync();
    }

    [RelayCommand]
    private async Task OpenAsync(LibraryEntry? entry)
    {
        if (entry is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object>
        {
            { "id", entry.StoryId }
        });
    }

    // "Đọc tiếp" -> mở lại chương đang đọc dở
    [RelayCommand]
    private async Task ContinueReadingAsync(LibraryEntry? entry)
    {
        if (entry is null || entry.LastChapterId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.ChapterReaderPage), new Dictionary<string, object>
        {
            { "chapterId", entry.LastChapterId }
        });
    }

    [RelayCommand]
    private async Task RemoveAsync(LibraryEntry? entry)
    {
        if (entry is null) return;
        if (IsTab3)
        {
            await RunSafeAsync(() => _progress.RemoveAsync(entry.StoryId));   // xóa cả trên server (Backend 2)
        }
        else if (IsTab0) _library.RemoveFromHistory(entry.StoryId);
        else _library.RemoveFavorite(entry.StoryId);
        await ShowAsync();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var ok = await Shell.Current.DisplayAlert("Xóa lịch sử", "Xóa toàn bộ truyện đã xem gần đây?", "Xóa", "Hủy");
        if (!ok) return;
        _library.ClearHistory();
        Show();
    }

    // ----- Mục "Đã mua" -----
    [RelayCommand]
    private async Task OpenOwnedStoryAsync(OwnedStory? story)
    {
        if (story is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object>
        {
            { "id", story.StoryId }
        });
    }

    [RelayCommand]
    private async Task OpenPlansAsync() => await Shell.Current.GoToAsync(nameof(Views.PlansPage));

    [RelayCommand]
    private async Task OpenHistoryAsync() => await Shell.Current.GoToAsync(nameof(Views.OrderHistoryPage));

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    private async Task ShowAsync()
    {
        if (SelectedTab == 2) await LoadPurchasesAsync();
        else if (SelectedTab == 3) await LoadReadingAsync();
        else Show();
    }

    private async Task LoadPurchasesAsync()
    {
        Items.Clear();
        ShowEmpty = false;
        HasItems = false;
        CountText = string.Empty;

        var loggedIn = _auth.CurrentUser is not null;
        PurchasesNeedLogin = !loggedIn;
        MyPlans.Clear();
        OwnedStories.Clear();
        HasMyPlans = false;
        HasOwnedStories = false;
        PurchasesEmpty = false;
        if (!loggedIn) return;

        await RunSafeAsync(async () =>
        {
            var purchases = await _access.GetPurchasesAsync();
            foreach (var p in purchases.Plans) MyPlans.Add(p);
            foreach (var s in purchases.Stories) OwnedStories.Add(s);
            HasMyPlans = MyPlans.Count > 0;
            HasOwnedStories = OwnedStories.Count > 0;
            PurchasesEmpty = !HasMyPlans && !HasOwnedStories;
            CountText = $"{MyPlans.Count} gói đang dùng · {OwnedStories.Count} truyện đã mua";
        });
    }

    // Mục "Đang đọc": các truyện đã đọc dở (có chương đang đọc), mới đọc nhất lên đầu
    private async Task LoadReadingAsync()
    {
        MyPlans.Clear();
        OwnedStories.Clear();
        Items.Clear();
        PurchasesNeedLogin = false;
        PurchasesEmpty = false;

        List<LibraryEntry> reading = new();
        await RunSafeAsync(async () => reading = await _progress.GetReadingAsync());
        foreach (var e in reading) Items.Add(e);

        HasItems = Items.Count > 0;
        ShowEmpty = !HasItems;
        CountText = HasItems ? $"{Items.Count} truyện đang đọc" : string.Empty;
        EmptyTitle = "Bạn chưa đọc dở truyện nào.";
        EmptyHint = "Đọc một chương bất kỳ, truyện sẽ hiện ở đây để bạn bấm “Đọc tiếp”.";
    }

    private void Show()
    {
        var source = IsTab0 ? _library.GetHistory() : _library.GetFavorites();

        Items.Clear();
        foreach (var entry in source) Items.Add(entry);

        HasItems = Items.Count > 0;
        ShowEmpty = !HasItems;
        CountText = HasItems ? $"{Items.Count} truyện" : string.Empty;
        EmptyTitle = IsTab0 ? "Bạn chưa có truyện nào đã xem." : "Bạn chưa lưu truyện nào.";
        EmptyHint = IsTab0
            ? "Mở một truyện bất kỳ, truyện sẽ hiện ở đây."
            : "Bấm “♡ Yêu thích” ở trang chi tiết truyện để lưu vào đây.";
    }
}

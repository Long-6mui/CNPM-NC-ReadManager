using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "Tủ sách" (làm lại theo ảnh tham khảo): ô tìm trong tủ sách + thanh tab gạch chân
//   Đang đọc | Theo dõi | Yêu thích | Đã mua | Đã xem
// - Đang đọc: truyện đọc dở (api/me/reading)          - Theo dõi: truyện đang theo dõi (api/me/follows)
// - Yêu thích: api/reader/favorites                   - Đã mua: gói đang dùng + truyện mua trọn bộ
// - Đã xem: lịch sử xem lưu trên máy
public partial class LibraryViewModel : BaseViewModel
{
    private const string Reading = "reading";
    private const string Follow = "follow";
    private const string Favorite = "favorite";
    private const string Bought = "bought";
    private const string History = "history";

    private readonly ILibraryService _library;
    private readonly IAuthService _auth;
    private readonly IReadingAccessService _access;
    private readonly ReaderActivityService _reader;
    private readonly IReadingProgressService _progress;
    private readonly ICommunityService _community;

    private List<LibraryEntry> _all = new();   // danh sách đầy đủ của tab đang chọn (trước khi lọc theo ô tìm)

    public ObservableCollection<ChipItem> Tabs { get; } = new()
    {
        new ChipItem(Reading, "Đang đọc") { IsSelected = true },
        new ChipItem(Follow, "Theo dõi"),
        new ChipItem(Favorite, "Yêu thích"),
        new ChipItem(Bought, "Đã mua"),
        new ChipItem(History, "Đã xem"),
    };

    public ObservableCollection<LibraryEntry> Items { get; } = new();

    // ----- Mục "Đã mua" -----
    public ObservableCollection<PlanEntitlement> MyPlans { get; } = new();
    public ObservableCollection<OwnedStory> OwnedStories { get; } = new();

    [ObservableProperty] private bool purchasesNeedLogin;
    [ObservableProperty] private bool purchasesEmpty;
    [ObservableProperty] private bool hasMyPlans;
    [ObservableProperty] private bool hasOwnedStories;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBought))]
    [NotifyPropertyChangedFor(nameof(ShowList))]
    [NotifyPropertyChangedFor(nameof(ShowClear))]
    [NotifyPropertyChangedFor(nameof(RemoveText))]
    private string currentTab = Reading;

    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private string emptyTitle = string.Empty;
    [ObservableProperty] private string emptyHint = string.Empty;
    [ObservableProperty] private string countText = string.Empty;
    [ObservableProperty] private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClear))]
    private bool hasItems;

    public bool IsBought => CurrentTab == Bought;
    public bool ShowList => !IsBought;
    public bool ShowClear => CurrentTab == History && HasItems;
    public string RemoveText => CurrentTab == Follow ? "Bỏ theo dõi" : "Xóa";

    public LibraryViewModel(ILibraryService library, IAuthService auth, IReadingAccessService access,
        IReadingProgressService progress, ReaderActivityService reader, ICommunityService community)
    {
        _reader = reader;
        _progress = progress;
        _library = library;
        _auth = auth;
        _access = access;
        _community = community;
    }

    // Gõ vào ô tìm -> lọc ngay danh sách đang hiện
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    // Gọi mỗi lần tab hiện ra để cập nhật sau khi đọc / theo dõi / yêu thích
    [RelayCommand]
    private async Task LoadAsync()
    {
        try { await _auth.IsLoggedInAsync(); } // nạp CurrentUser để chọn đúng tủ sách của tài khoản
        catch { /* mất mạng: dùng tủ sách theo thông tin hiện có */ }
        await ShowAsync();
    }

    [RelayCommand]
    private async Task SelectTabAsync(ChipItem? tab)
    {
        if (tab is null || tab.IsSelected) return;
        foreach (var t in Tabs) t.IsSelected = ReferenceEquals(t, tab);
        CurrentTab = tab.Key;
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
        if (CurrentTab == Follow &&
            !await Shell.Current.DisplayAlert("Bỏ theo dõi", $"Bỏ theo dõi “{entry.Title}”? Bạn sẽ không nhận thông báo chương mới nữa.", "Bỏ theo dõi", "Hủy"))
            return;

        await RunSafeAsync(async () =>
        {
            switch (CurrentTab)
            {
                case Reading: await _progress.RemoveAsync(entry.StoryId); break;
                case History: _library.RemoveFromHistory(entry.StoryId); break;
                case Follow: await _community.SetFollowAsync(entry.StoryId, false); break;
                default:
                    if (_auth.CurrentUser is not null) await _reader.SetFavorite(entry.StoryId, false);
                    else _library.RemoveFavorite(entry.StoryId);
                    break;
            }
        });
        await ShowAsync();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var ok = await Shell.Current.DisplayAlert("Xóa lịch sử", "Xóa toàn bộ truyện đã xem gần đây?", "Xóa", "Hủy");
        if (!ok) return;
        _library.ClearHistory();
        await ShowAsync();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private async Task GoToSearchAsync() => await Shell.Current.GoToAsync(nameof(Views.SearchPage));

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

    // ===================== TẢI TỪNG TAB =====================
    private async Task ShowAsync()
    {
        ErrorMessage = null;
        switch (CurrentTab)
        {
            case Bought: await LoadPurchasesAsync(); return;
            case Reading: await LoadReadingAsync(); return;
            case Follow: await LoadFollowsAsync(); return;
            case Favorite:
                List<LibraryEntry> favs = new();
                await RunSafeAsync(async () => favs = (await _reader.Favorites()).ToList());
                foreach (var f in favs) f.Note = f.SavedText;
                SetList(favs, "Bạn chưa lưu truyện nào.", "Bấm “♡ Yêu thích” ở trang chi tiết truyện để lưu vào đây.");
                return;
            default:
                var history = _library.GetHistory().ToList();
                foreach (var h in history) h.Note = $"{h.ProgressText} · {h.ViewedText}";
                SetList(history, "Bạn chưa xem truyện nào.", "Mở một truyện bất kỳ, truyện sẽ hiện ở đây.");
                return;
        }
    }

    // "Đang đọc": truyện đọc dở, mới đọc nhất lên đầu
    private async Task LoadReadingAsync()
    {
        List<LibraryEntry> reading = new();
        await RunSafeAsync(async () => reading = await _progress.GetReadingAsync());
        foreach (var e in reading) e.Note = $"{e.ProgressText} · {e.ViewedText}";
        SetList(reading, "Chưa có truyện nào đang đọc.", "Đọc một chương bất kỳ, truyện sẽ hiện ở đây để bạn bấm “Đọc tiếp”.");
    }

    // "Theo dõi": có chương mới sẽ nhận thông báo (chuông 🔔 trên header)
    private async Task LoadFollowsAsync()
    {
        var follows = new List<LibraryEntry>();
        if (_auth.CurrentUser is not null)
        {
            await RunSafeAsync(async () =>
            {
                var list = await _community.GetMyFollowsAsync();
                follows = list.Select(f => new LibraryEntry
                {
                    StoryId = f.StoryId,
                    Title = f.Title,
                    CoverUrl = f.CoverUrl,
                    SavedAt = f.FollowedAt,
                    Note = $"🔔 {f.FollowedText} · báo khi có chương mới"
                }).ToList();
            });
        }
        SetList(follows,
            _auth.CurrentUser is null ? "Đăng nhập để theo dõi truyện." : "Bạn chưa theo dõi truyện nào.",
            "Bấm “Theo dõi” ở trang chi tiết truyện để nhận thông báo khi có chương mới.");
    }

    private async Task LoadPurchasesAsync()
    {
        _all.Clear();
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

    private void SetList(List<LibraryEntry> entries, string emptyTitle, string emptyHint)
    {
        _all = entries;
        EmptyTitle = emptyTitle;
        EmptyHint = emptyHint;
        ApplyFilter();
    }

    // Lọc theo ô tìm (tên truyện hoặc tác giả, không phân biệt hoa thường)
    private void ApplyFilter()
    {
        if (IsBought) return;
        var q = SearchText.Trim();
        var shown = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(e => e.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                           || e.AuthorName.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();

        Items.Clear();
        foreach (var e in shown) Items.Add(e);

        HasItems = Items.Count > 0;
        ShowEmpty = !HasItems;
        CountText = !HasItems ? string.Empty
            : string.IsNullOrEmpty(q) ? $"{Items.Count} truyện" : $"Tìm thấy {Items.Count} truyện";
        if (!HasItems && _all.Count > 0)
        {
            EmptyTitle = "Không có truyện khớp từ khóa.";
            EmptyHint = "Thử gõ tên truyện hoặc tác giả khác.";
        }
    }
}

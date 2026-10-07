using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Trang chủ + Khám phá (gộp một màn như web: Home/Index + Stories/Browse)
public partial class StoryListViewModel : BaseViewModel
{
    private const int HomePageSize = 12;    // web: ListAsync mặc định pageSize = 12
    private const int BrowsePageSize = 48;  // web: Browse lấy 48 truyện

    private readonly IStoryService _storyService;
    private readonly IAuthService _authService;

    private List<StorySummary> _newest = new();
    private List<StorySummary> _free = new();
    private Genre? _genre;
    private string? _access;

    public ObservableCollection<StorySummary> Updated { get; } = new();    // "Mới cập nhật" + bảng "Mới lên chương"
    public ObservableCollection<StorySummary> TabStories { get; } = new(); // truyện của tab đang chọn
    public ObservableCollection<StorySummary> Results { get; } = new();    // kết quả khi tìm/lọc
    public ObservableCollection<Genre> GenreCats { get; } = new();         // 6 thể loại đầu (như web)

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHome))]
    private bool isFiltering;

    [ObservableProperty] private string filterText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTab0))]
    [NotifyPropertyChangedFor(nameof(IsTab1))]
    [NotifyPropertyChangedFor(nameof(IsTab2))]
    private int selectedTab;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    private bool isLoggedIn;

    [ObservableProperty] private int totalStories;
    [ObservableProperty] private int totalGenres;
    [ObservableProperty] private int totalChapters;
    [ObservableProperty] private int freeCount;
    [ObservableProperty] private bool hasGenres;
    [ObservableProperty] private bool hasStories;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private bool showNoResults;

    public bool IsHome => !IsFiltering;
    public bool IsGuest => !IsLoggedIn;
    public bool IsTab0 => SelectedTab == 0;
    public bool IsTab1 => SelectedTab == 1;
    public bool IsTab2 => SelectedTab == 2;

    public StoryListViewModel(IStoryService storyService, IAuthService authService)
    {
        _storyService = storyService;
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunSafeAsync(async () => IsLoggedIn = await _authService.IsLoggedInAsync());
        if (IsFiltering || Updated.Count > 0) return; // đã có dữ liệu thì không tải lại (kéo xuống để làm mới)
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await ReloadAsync();
        IsRefreshing = false;
    }

    // Enter ở ô tìm kiếm
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText) && _genre is null && _access is null)
        {
            await ClearFilterAsync();
            return;
        }
        IsFiltering = true;
        UpdateFilterText();
        await ReloadAsync();
    }

    // Bấm thẻ thể loại — web: Stories/Browse?genre=
    [RelayCommand]
    private async Task FilterByGenreAsync(Genre? genre)
    {
        _genre = genre;
        _access = null;
        IsFiltering = true;
        UpdateFilterText();
        await ReloadAsync();
    }

    // "Khám phá thư viện" (access rỗng) và "Đọc miễn phí" (access = Free) — web: Stories/Browse
    [RelayCommand]
    private async Task BrowseAsync(string? access)
    {
        _genre = null;
        _access = string.IsNullOrWhiteSpace(access) ? null : access;
        SearchText = string.Empty;
        IsFiltering = true;
        UpdateFilterText();
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        _genre = null;
        _access = null;
        SearchText = string.Empty;
        FilterText = string.Empty;
        IsFiltering = false;
        Results.Clear();
        ShowNoResults = false;
        if (Updated.Count == 0) await ReloadAsync();
    }

    [RelayCommand]
    private void SelectTab(string? index)
    {
        SelectedTab = int.TryParse(index, out var i) ? i : 0;
        ShowTab();
    }

    [RelayCommand]
    private async Task GoToDetailAsync(StorySummary? story)
    {
        if (story is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object>
        {
            { "id", story.StoryId }
        });
    }

    // Sang tab Khám phá (web: liên kết tới Stories/Browse)
    [RelayCommand]
    private async Task GoToBrowseAsync() => await Shell.Current.GoToAsync("//BrowsePage");

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await RunSafeAsync(() => _authService.LogoutAsync());
        IsLoggedIn = false;
    }

    // ---------------------------------------------------------------

    private Task ReloadAsync() => RunSafeAsync(async () =>
    {
        ShowEmpty = false;
        if (IsFiltering) await LoadResultsAsync();
        else await LoadHomeAsync();
    });

    private async Task LoadHomeAsync()
    {
        var genres = await _storyService.GetGenresAsync();
        GenreCats.Clear();
        foreach (var g in genres.Take(6)) GenreCats.Add(g);
        TotalGenres = genres.Count;
        HasGenres = GenreCats.Count > 0;

        // Giống HomeController.Index của web: 3 danh sách updated / newest / free
        var updated = await _storyService.GetListAsync(null, null, "updated", null, 1, HomePageSize);
        var newest = await _storyService.GetListAsync(null, null, "newest", null, 1, HomePageSize);
        var free = await _storyService.GetListAsync(null, null, "updated", "Free", 1, HomePageSize);

        Replace(Updated, updated.Items);
        _newest = newest.Items;
        _free = free.Items;

        TotalStories = updated.TotalItems;
        TotalChapters = updated.Items.Sum(s => s.PublishedChapterCount);
        FreeCount = free.Items.Count;

        ShowTab();
        HasStories = Updated.Count > 0;
        ShowEmpty = !HasStories;
    }

    private async Task LoadResultsAsync()
    {
        var q = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
        var page = await _storyService.GetListAsync(q, _genre?.GenreId, "updated", _access, 1, BrowsePageSize);
        Replace(Results, page.Items);
        ShowNoResults = Results.Count == 0;
    }

    private void ShowTab()
    {
        IEnumerable<StorySummary> source = SelectedTab switch
        {
            1 => _newest,
            2 => _free,
            _ => Updated.ToList()
        };
        Replace(TabStories, source);
    }

    private void UpdateFilterText()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(SearchText)) parts.Add($"“{SearchText.Trim()}”");
        if (_genre is not null) parts.Add(_genre.Name);
        if (_access == "Free") parts.Add("Đọc miễn phí");
        FilterText = parts.Count == 0 ? "Tất cả truyện" : string.Join(" · ", parts);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        target.Clear();
        foreach (var item in list) target.Add(item);
    }
}

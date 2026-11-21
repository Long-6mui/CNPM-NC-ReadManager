using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// TRANG TÌM KIẾM (mở bằng nút 🔍 trên header) — tham khảo ảnh mẫu:
//  - Chưa tìm: ô tìm kiếm + "Danh mục" (chip thể loại) + từ khóa đang hot.
//  - Đã tìm / bấm thể loại: thanh lọc (bộ lọc ⚙, thể loại ▾, sắp xếp ▾), "Tìm thấy N truyện",
//    đổi kiểu xem danh sách / lưới.
// Dùng API có sẵn: GET api/stories?q=&genreId=&status=&access=&sort=, GET api/genres, GET api/discovery/trending
public partial class SearchViewModel : BaseViewModel
{
    private const int PageSize = 48;

    private readonly IStoryService _storyService;
    private readonly ApiClient _api;
    private bool _loaded;
    private int _searchLoad;

    private static readonly (string Key, string Name)[] Sorts =
    {
        ("updated", "Mới cập nhật"), ("newest", "Mới đăng"), ("reads", "Đọc nhiều"),
        ("follows", "Theo dõi nhiều"), ("title", "Tên A–Z")
    };

    // Bộ lọc ⚙: (tên hiển thị, access, status)
    private static readonly (string Name, string? Access, string? Status)[] Filters =
    {
        ("Tất cả truyện", null, null),
        ("Miễn phí", "Free", null),
        ("Kết hợp", "Mixed", null),
        ("Trả phí", "Paid", null),
        ("Đang ra", null, "Ongoing"),
        ("Hoàn thành", null, "Completed"),
    };

    public ObservableCollection<ChipItem> Categories { get; } = new();
    public ObservableCollection<string> Keywords { get; } = new();
    public ObservableCollection<StorySummary> Results { get; } = new();

    [ObservableProperty] private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrowsing))]
    private bool hasQueried;

    [ObservableProperty] private string genreName = "Tất cả thể loại";
    [ObservableProperty] private bool hasGenre;
    [ObservableProperty] private string sortName = "Mới cập nhật";
    [ObservableProperty] private string filterName = "Tất cả truyện";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterIconBg))]
    private bool hasFilter;
    [ObservableProperty] private string countText = string.Empty;
    [ObservableProperty] private bool showNoResults;
    [ObservableProperty] private bool hasKeywords;
    [ObservableProperty] private bool isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsList))]
    [NotifyPropertyChangedFor(nameof(ListTabBg))]
    [NotifyPropertyChangedFor(nameof(GridTabBg))]
    private bool isGrid;

    public bool IsBrowsing => !HasQueried;
    public bool IsList => !IsGrid;

    // Màu tính sẵn (không dùng DataTrigger + DynamicResource — xem Models/ChipItem.cs)
    public Color FilterIconBg => ThemeService.Get(HasFilter ? "WebPill" : "WebSurface");
    public Color ListTabBg => IsList ? ThemeService.Get("WebSurface") : Colors.Transparent;
    public Color GridTabBg => IsGrid ? ThemeService.Get("WebSurface") : Colors.Transparent;

    private int? _genreId;
    private string _sortKey = "updated";
    private string? _access;
    private string? _status;

    public SearchViewModel(IStoryService storyService, ApiClient api)
    {
        _storyService = storyService;
        _api = api;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_loaded) return;
        await RunSafeAsync(async () =>
        {
            var genres = await _storyService.GetGenresAsync();
            Categories.Clear();
            foreach (var g in genres) Categories.Add(new ChipItem(g.Slug, g.Name, g.GenreId));
            _loaded = true;
        });

        // Từ khóa đang được tìm nhiều (không có cũng không sao)
        try
        {
            var trending = await _api.GetAsync<TrendingResponse>("discovery/trending");
            Keywords.Clear();
            foreach (var k in trending?.Keywords ?? new List<string>()) Keywords.Add(k);
            HasKeywords = Keywords.Count > 0;
        }
        catch { HasKeywords = false; }
    }

    // Enter ở ô tìm kiếm
    [RelayCommand]
    private Task SearchAsync() => RunSearchAsync();

    [RelayCommand]
    private Task SearchKeywordAsync(string? keyword)
    {
        SearchText = keyword ?? string.Empty;
        return RunSearchAsync();
    }

    // Bấm một thể loại trong "Danh mục"
    [RelayCommand]
    private Task PickCategoryAsync(ChipItem? chip)
    {
        if (chip is null) return Task.CompletedTask;
        SetGenre(chip.Id, chip.Name);
        return RunSearchAsync();
    }

    // Chip thể loại "Đô Thị ▾" trên thanh lọc
    [RelayCommand]
    private async Task PickGenreAsync()
    {
        var names = new[] { "Tất cả thể loại" }.Concat(Categories.Select(c => c.Name)).ToArray();
        var picked = await Shell.Current.DisplayActionSheet("Thể loại", "Đóng", null, names);
        if (picked is null || picked == "Đóng") return;
        var chip = Categories.FirstOrDefault(c => c.Name == picked);
        SetGenre(chip?.Id, chip?.Name ?? "Tất cả thể loại");
        await RunSearchAsync();
    }

    [RelayCommand]
    private async Task PickSortAsync()
    {
        var picked = await Shell.Current.DisplayActionSheet("Sắp xếp theo", "Đóng", null, Sorts.Select(s => s.Name).ToArray());
        var sort = Sorts.FirstOrDefault(s => s.Name == picked);
        if (sort.Key is null) return;
        _sortKey = sort.Key;
        SortName = sort.Name;
        await RunSearchAsync();
    }

    [RelayCommand]
    private async Task PickFilterAsync()
    {
        var picked = await Shell.Current.DisplayActionSheet("Bộ lọc", "Đóng", null, Filters.Select(f => f.Name).ToArray());
        var filter = Filters.FirstOrDefault(f => f.Name == picked);
        if (filter.Name is null) return;
        _access = filter.Access;
        _status = filter.Status;
        FilterName = filter.Name;
        HasFilter = filter.Access is not null || filter.Status is not null;
        await RunSearchAsync();
    }

    [RelayCommand] private void ShowList() => IsGrid = false;
    [RelayCommand] private void ShowGrid() => IsGrid = true;

    [RelayCommand]
    private void ClearText() => SearchText = string.Empty;

    // "Hủy": đang xem kết quả -> quay về danh mục; đang ở danh mục -> đóng trang
    [RelayCommand]
    private async Task CancelAsync()
    {
        if (HasQueried)
        {
            HasQueried = false;
            SearchText = string.Empty;
            SetGenre(null, "Tất cả thể loại");
            Results.Clear();
            return;
        }
        await Shell.Current.GoToAsync("..");
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

    // ---------------------------------------------------------------

    private void SetGenre(int? id, string name)
    {
        _genreId = id;
        GenreName = id.HasValue ? name : "Tất cả thể loại";
        HasGenre = id.HasValue;
    }

    private async Task RunSearchAsync()
    {
        var load = ++_searchLoad;
        HasQueried = true;
        IsSearching = true;
        ErrorMessage = null;
        try
        {
            var q = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var page = await _storyService.GetListAsync(q, _genreId, _status, _access, _sortKey, 1, PageSize);
            if (load != _searchLoad) return;

            Results.Clear();
            foreach (var s in page.Items) Results.Add(s);
            var total = Math.Max(page.TotalItems, Results.Count);
            CountText = $"{total} truyện";
            ShowNoResults = Results.Count == 0;
        }
        catch (ApiException ex) { ErrorMessage = ex.Message; }
        catch (Exception) { ErrorMessage = $"Không kết nối được máy chủ ({ApiSettings.BaseUrl})."; }
        finally
        {
            if (load == _searchLoad) IsSearching = false;
        }
    }

    // GET api/discovery/trending (chỉ cần phần từ khóa)
    private sealed class TrendingResponse
    {
        public List<string> Keywords { get; set; } = new();
    }
}

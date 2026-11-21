using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// TRANG CHỦ
//  1. Hero "sách bay" (truyện mới cập nhật)
//  2. Thống kê nhanh
//  3. Đề xuất cho bạn (3 hàng x 3 truyện theo sở thích, nút "Xem tất cả")
//  4. Bảng xếp hạng (thay cho trang Xếp hạng cũ) + lọc theo thể loại + tiêu chí (lượt đọc / theo dõi / tổng hợp) + kỳ
//  5. Truyện nổi bật (3 tab) và Mới lên chương
// Tìm kiếm đã chuyển sang trang riêng (nút 🔍 trên header).
public partial class StoryListViewModel : BaseViewModel
{
    private const int HomePageSize = 12;      // web: ListAsync mặc định pageSize = 12
    private const int RecommendPreview = 9;   // 3 hàng x 3 truyện
    private const int RankingTop = 10;        // hạng 1 (thẻ lớn) + 9 truyện (3 hàng x 3)

    private readonly IStoryService _storyService;
    private readonly IAuthService _authService;
    private readonly RankingService _ranking;
    private readonly RecommendationService _recommend;

    private List<StorySummary> _newest = new();
    private List<StorySummary> _free = new();
    private int _rankingLoad;          // chống kết quả cũ ghi đè khi bấm chip liên tục
    private int _recommendLoad;

    public ObservableCollection<StorySummary> Updated { get; } = new();          // hero + "Mới lên chương"
    public ObservableCollection<StorySummary> TabStories { get; } = new();       // tab "Truyện nổi bật" đang chọn
    public ObservableCollection<StorySummary> Recommended { get; } = new();      // 9 truyện đề xuất
    public ObservableCollection<StorySummary> RankingRest { get; } = new();      // hạng 2..10

    public ObservableCollection<ChipItem> RankGenres { get; } = new();           // Tất cả + các thể loại
    public ObservableCollection<ChipItem> RankMetrics { get; } = new()
    {
        new ChipItem("reads", "Lượt đọc") { IsSelected = true },
        new ChipItem("follows", "Theo dõi nhiều"),
        new ChipItem("score", "Nổi bật nhất"),
    };

    private static readonly (string Key, string Name)[] Periods =
    {
        ("day", "Hôm nay"), ("week", "Tuần"), ("month", "Tháng"), ("all", "Mọi lúc")
    };

    private List<Genre> _allGenres = new();

    [ObservableProperty] private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTab0))]
    [NotifyPropertyChangedFor(nameof(IsTab1))]
    [NotifyPropertyChangedFor(nameof(IsTab2))]
    private int selectedTab;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    [NotifyPropertyChangedFor(nameof(ShowPickPreferences))]
    private bool isLoggedIn;

    [ObservableProperty] private int totalStories;
    [ObservableProperty] private int totalGenres;
    [ObservableProperty] private int totalChapters;
    [ObservableProperty] private int freeCount;
    [ObservableProperty] private bool hasStories;
    [ObservableProperty] private bool showEmpty;

    // Đề xuất
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPickPreferences))]
    private bool hasPreferences;
    [ObservableProperty] private bool hasRecommendations;
    [ObservableProperty] private bool isRecommendBusy;
    [ObservableProperty] private string recommendCaption = string.Empty;

    // Xếp hạng
    [ObservableProperty] private StorySummary? rankingTop;
    [ObservableProperty] private bool hasRanking;
    [ObservableProperty] private bool isRankingBusy;
    [ObservableProperty] private bool showRankingEmpty;
    [ObservableProperty] private string periodKey = "week";
    [ObservableProperty] private string periodName = "Tuần";

    public bool IsGuest => !IsLoggedIn;
    public bool ShowPickPreferences => IsLoggedIn && !HasPreferences;
    public bool IsTab0 => SelectedTab == 0;
    public bool IsTab1 => SelectedTab == 1;
    public bool IsTab2 => SelectedTab == 2;

    public StoryListViewModel(IStoryService storyService, IAuthService authService,
        RankingService ranking, RecommendationService recommend)
    {
        _storyService = storyService;
        _authService = authService;
        _ranking = ranking;
        _recommend = recommend;
    }

    // ===================== TẢI DỮ LIỆU =====================

    // Gọi mỗi lần trang hiện ra
    [RelayCommand]
    private async Task LoadAsync()
    {
        try { IsLoggedIn = await _authService.IsLoggedInAsync(); }
        catch { IsLoggedIn = _authService.CurrentUser is not null; }

        if (Updated.Count == 0) await ReloadAsync();     // lần đầu (sau đó kéo xuống để làm mới)
        await LoadRecommendationsAsync();                 // luôn tải lại: có thể vừa đăng nhập / đổi sở thích
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await ReloadAsync();
        await LoadRecommendationsAsync();
        IsRefreshing = false;
    }

    private Task ReloadAsync() => RunSafeAsync(async () =>
    {
        ShowEmpty = false;

        var genres = await _storyService.GetGenresAsync();
        _allGenres = genres;
        TotalGenres = genres.Count;
        BuildGenreChips();

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

        _ = LoadRankingAsync();
    });

    // ----- Đề xuất cho bạn -----
    public async Task LoadRecommendationsAsync()
    {
        var load = ++_recommendLoad;
        if (!IsLoggedIn)
        {
            Recommended.Clear();
            HasPreferences = false;
            HasRecommendations = false;
            return;
        }

        IsRecommendBusy = Recommended.Count == 0;
        try
        {
            var ids = await _recommend.GetPreferredGenreIdsAsync();
            if (load != _recommendLoad) return;
            HasPreferences = ids.Length > 0;
            if (!HasPreferences)
            {
                Recommended.Clear();
                HasRecommendations = false;
                return;
            }

            var names = ids.Select(id => _allGenres.FirstOrDefault(g => g.GenreId == id)?.Name)
                           .Where(n => !string.IsNullOrEmpty(n)).Take(3).ToList();
            RecommendCaption = names.Count > 0 ? $"Theo sở thích: {string.Join(", ", names)}{(ids.Length > 3 ? "…" : "")}" : "Theo sở thích của bạn";

            var stories = await _recommend.GetStoriesAsync(ids, perGenre: 12);
            if (load != _recommendLoad) return;
            Replace(Recommended, stories.Take(RecommendPreview));
            HasRecommendations = Recommended.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
        finally
        {
            if (load == _recommendLoad) IsRecommendBusy = false;
        }
    }

    // ----- Bảng xếp hạng -----
    private void BuildGenreChips()
    {
        var selectedId = RankGenres.FirstOrDefault(c => c.IsSelected)?.Id;
        RankGenres.Clear();
        RankGenres.Add(new ChipItem("all", "Tất cả") { IsSelected = selectedId is null });
        foreach (var g in _allGenres)
            RankGenres.Add(new ChipItem(g.Slug, g.Name, g.GenreId) { IsSelected = selectedId == g.GenreId });
    }

    private async Task LoadRankingAsync()
    {
        var load = ++_rankingLoad;
        IsRankingBusy = true;
        try
        {
            var by = RankMetrics.FirstOrDefault(c => c.IsSelected)?.Key ?? "reads";
            var genreId = RankGenres.FirstOrDefault(c => c.IsSelected)?.Id;
            var items = await _ranking.GetStoriesAsync(by, PeriodKey, genreId, RankingTop);
            if (load != _rankingLoad) return;

            foreach (var s in items) s.MetricText = MetricLabel(by, s);

            var top = items.FirstOrDefault();
            if (top is not null)
            {
                // Mô tả ngắn cho thẻ hạng 1 (API danh sách không trả tóm tắt nên lấy từ chi tiết truyện)
                try
                {
                    var detail = await _storyService.GetStoryByIdAsync(top.StoryId);
                    top.Blurb = string.IsNullOrWhiteSpace(detail.Synopsis) ? top.GenreLine : detail.Synopsis.Trim();
                }
                catch { top.Blurb = top.GenreLine; }
                if (load != _rankingLoad) return;
            }

            RankingTop = top;
            Replace(RankingRest, items.Skip(1));
            HasRanking = top is not null;
            ShowRankingEmpty = top is null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
            if (load == _rankingLoad) { HasRanking = false; ShowRankingEmpty = true; }
        }
        finally
        {
            if (load == _rankingLoad) IsRankingBusy = false;
        }
    }

    private static string MetricLabel(string by, StorySummary s) => by switch
    {
        "follows" => $"♥ {s.TotalFollows:N0} theo dõi",
        "score" => $"👁 {s.TotalReads:N0}  ·  ♥ {s.TotalFollows:N0}",
        _ => $"👁 {s.TotalReads:N0} lượt đọc"
    };

    [RelayCommand]
    private async Task SelectRankGenreAsync(ChipItem? chip)
    {
        if (chip is null || chip.IsSelected) return;
        foreach (var c in RankGenres) c.IsSelected = ReferenceEquals(c, chip);
        await LoadRankingAsync();
    }

    [RelayCommand]
    private async Task SelectRankMetricAsync(ChipItem? chip)
    {
        if (chip is null || chip.IsSelected) return;
        foreach (var c in RankMetrics) c.IsSelected = ReferenceEquals(c, chip);
        await LoadRankingAsync();
    }

    // Nút "▾" cạnh hàng thể loại: chọn nhanh trong danh sách đầy đủ
    [RelayCommand]
    private async Task PickRankGenreAsync()
    {
        if (RankGenres.Count == 0) return;
        var names = RankGenres.Select(c => c.Name).ToArray();
        var picked = await Shell.Current.DisplayActionSheet("Chọn thể loại", "Đóng", null, names);
        var chip = RankGenres.FirstOrDefault(c => c.Name == picked);
        if (chip is not null) await SelectRankGenreAsync(chip);
    }

    // Kỳ xếp hạng: Hôm nay / Tuần / Tháng / Mọi lúc
    [RelayCommand]
    private async Task PickPeriodAsync()
    {
        var picked = await Shell.Current.DisplayActionSheet("Xếp hạng theo", "Đóng", null, Periods.Select(p => p.Name).ToArray());
        var period = Periods.FirstOrDefault(p => p.Name == picked);
        if (period.Key is null || period.Key == PeriodKey) return;
        PeriodKey = period.Key;
        PeriodName = period.Name;
        await LoadRankingAsync();
    }

    // ===================== ĐIỀU HƯỚNG =====================

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

    [RelayCommand]
    private async Task GoToSearchAsync() => await Shell.Current.GoToAsync(nameof(Views.SearchPage));

    [RelayCommand]
    private async Task GoToRecommendedAsync() => await Shell.Current.GoToAsync(nameof(Views.RecommendedPage));

    [RelayCommand]
    private async Task GoToPreferencesAsync() => await Shell.Current.GoToAsync(nameof(Views.ReaderGenresPage));

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));

    // ---------------------------------------------------------------

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

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        target.Clear();
        foreach (var item in list) target.Add(item);
    }
}

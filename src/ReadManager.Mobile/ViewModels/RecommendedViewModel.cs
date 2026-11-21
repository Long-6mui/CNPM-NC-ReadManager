using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// Trang "ĐỀ XUẤT CHO BẠN" (bấm "Xem tất cả" ở trang chủ): toàn bộ truyện theo sở thích,
// lọc nhanh theo từng thể loại đã chọn.
public partial class RecommendedViewModel : BaseViewModel
{
    private readonly RecommendationService _recommend;
    private readonly IStoryService _storyService;
    private int[] _genreIds = Array.Empty<int>();
    private int _load;

    public ObservableCollection<ChipItem> GenreChips { get; } = new();
    public ObservableCollection<StorySummary> Stories { get; } = new();

    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private bool showNoPreferences;
    [ObservableProperty] private string countText = string.Empty;

    public RecommendedViewModel(RecommendationService recommend, IStoryService storyService)
    {
        _recommend = recommend;
        _storyService = storyService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            _genreIds = await _recommend.GetPreferredGenreIdsAsync();
            ShowNoPreferences = _genreIds.Length == 0;

            var genres = await _storyService.GetGenresAsync();
            var selected = GenreChips.FirstOrDefault(c => c.IsSelected)?.Id;
            GenreChips.Clear();
            GenreChips.Add(new ChipItem("all", "Tất cả") { IsSelected = selected is null });
            foreach (var g in genres.Where(g => _genreIds.Contains(g.GenreId)))
                GenreChips.Add(new ChipItem(g.Slug, g.Name, g.GenreId) { IsSelected = selected == g.GenreId });
            if (!GenreChips.Any(c => c.IsSelected)) GenreChips[0].IsSelected = true;
        });
        await LoadStoriesAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task SelectGenreAsync(ChipItem? chip)
    {
        if (chip is null || chip.IsSelected) return;
        foreach (var c in GenreChips) c.IsSelected = ReferenceEquals(c, chip);
        await LoadStoriesAsync();
    }

    private async Task LoadStoriesAsync()
    {
        var load = ++_load;
        if (_genreIds.Length == 0)
        {
            Stories.Clear();
            ShowEmpty = false;
            CountText = string.Empty;
            return;
        }
        try
        {
            var only = GenreChips.FirstOrDefault(c => c.IsSelected)?.Id;
            var items = await _recommend.GetStoriesAsync(_genreIds, only, perGenre: 30);
            if (load != _load) return;
            Stories.Clear();
            foreach (var s in items) Stories.Add(s);
            CountText = $"{Stories.Count} truyện hợp gu bạn";
            ShowEmpty = Stories.Count == 0;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = ex is ApiException api ? api.Message : $"Không kết nối được máy chủ ({ApiSettings.BaseUrl}).";
        }
    }

    [RelayCommand]
    private async Task EditPreferencesAsync() => await Shell.Current.GoToAsync(nameof(Views.ReaderGenresPage));

    [RelayCommand]
    private async Task GoToDetailAsync(StorySummary? story)
    {
        if (story is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object>
        {
            { "id", story.StoryId }
        });
    }
}

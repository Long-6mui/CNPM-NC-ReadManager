using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// Dùng cho Trang chủ / Danh sách truyện
public partial class StoryListViewModel : BaseViewModel
{
    private readonly IStoryService _storyService;
    private int _currentPage = 1;
    private int _totalPages = 1;

    public ObservableCollection<StorySummary> Stories { get; } = new();
    public ObservableCollection<Genre> Genres { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private Genre? selectedGenre;

    [ObservableProperty]
    private bool isRefreshing;

    public StoryListViewModel(IStoryService storyService)
    {
        _storyService = storyService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Stories.Count > 0) return; // đã có dữ liệu rồi thì không load lại (dùng RefreshCommand để tải mới)
        await RunSafeAsync(async () =>
        {
            await LoadGenresInternalAsync();
            await LoadStoriesInternalAsync(resetList: true);
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RunSafeAsync(async () =>
        {
            IsRefreshing = true;
            await LoadStoriesInternalAsync(resetList: true);
        });
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await RunSafeAsync(() => LoadStoriesInternalAsync(resetList: true));
    }

    [RelayCommand]
    private async Task FilterByGenreAsync(Genre? genre)
    {
        SelectedGenre = genre;
        await RunSafeAsync(() => LoadStoriesInternalAsync(resetList: true));
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_currentPage >= _totalPages) return;
        await RunSafeAsync(() => LoadStoriesInternalAsync(resetList: false));
    }

    [RelayCommand]
    private async Task GoToDetailAsync(StorySummary? story)
    {
        if (story is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object>
        {
            { "slug", story.Slug }
        });
    }

    private async Task LoadGenresInternalAsync()
    {
        var genres = await _storyService.GetGenresAsync();
        Genres.Clear();
        foreach (var g in genres) Genres.Add(g);
    }

    private async Task LoadStoriesInternalAsync(bool resetList)
    {
        if (resetList) _currentPage = 1;

        var page = await _storyService.GetStoriesAsync(
            page: resetList ? 1 : _currentPage + 1,
            search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            genreId: SelectedGenre?.GenreId);

        _currentPage = page.Page;
        _totalPages = Math.Max(page.TotalPages, 1);

        if (resetList) Stories.Clear();
        foreach (var s in page.Items) Stories.Add(s);
    }
}

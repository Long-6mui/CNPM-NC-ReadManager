using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// Trang "MỚI RA MẮT" (bấm "Xem thêm" ở trang chủ): truyện mới đăng, cuộn xuống tự tải thêm.
// API: GET api/stories?sort=newest&page=&pageSize=
public partial class NewReleasesViewModel : BaseViewModel
{
    private const int PageSize = 20;
    private readonly IStoryService _storyService;
    private int _page;
    private int _totalItems;
    private bool _loadingMore;

    public ObservableCollection<StorySummary> Stories { get; } = new();

    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private bool isLoadingMore;
    [ObservableProperty] private bool showEmpty;

    public NewReleasesViewModel(IStoryService storyService) => _storyService = storyService;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Stories.Count > 0) return;
        await RunSafeAsync(() => LoadPageAsync(reset: true));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await RunSafeAsync(() => LoadPageAsync(reset: true));
        IsRefreshing = false;
    }

    // Cuộn gần cuối danh sách -> tải trang tiếp theo
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_loadingMore || IsBusy || Stories.Count >= _totalItems) return;
        _loadingMore = true;
        IsLoadingMore = true;
        try { await LoadPageAsync(reset: false); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally
        {
            _loadingMore = false;
            IsLoadingMore = false;
        }
    }

    private async Task LoadPageAsync(bool reset)
    {
        var next = reset ? 1 : _page + 1;
        var result = await _storyService.GetListAsync(null, null, "newest", null, next, PageSize);
        if (reset) Stories.Clear();
        foreach (var s in result.Items) Stories.Add(s);
        _page = next;
        _totalItems = result.TotalItems;
        ShowEmpty = Stories.Count == 0;
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
}

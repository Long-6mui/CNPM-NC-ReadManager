using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// Tab "Khám phá" — giống Views/Stories/Browse.cshtml của web:
// ô tìm kiếm + 4 bộ lọc (thể loại, hình thức, tình trạng, sắp xếp) + lưới thẻ truyện.
public partial class BrowseViewModel : BaseViewModel
{
    private const int PageSize = 48; // web: Browse lấy 48 truyện

    private readonly IStoryService _storyService;
    private List<Genre> _genres = new();
    private bool _genresLoaded;
    private bool _suppress;      // true khi tự gán chỉ số cho Picker, để không kích hoạt tải lại
    private bool _reloadQueued;  // có thay đổi lọc trong lúc đang tải -> tải lại một lần nữa

    // Danh sách lựa chọn — chữ đúng như các <select> của web
    public string[] AccessOptions { get; } = { "Mọi hình thức", "Miễn phí", "Kết hợp", "Trả phí" };
    public string[] StatusOptions { get; } = { "Mọi tình trạng", "Đang ra", "Hoàn thành", "Tạm ngừng" };
    public string[] SortOptions { get; } = { "Mới cập nhật", "Mới đăng", "Tên A–Z" };
    public ObservableCollection<string> GenreOptions { get; } = new() { "Tất cả thể loại" };

    public ObservableCollection<StorySummary> Stories { get; } = new();

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private int selectedGenreIndex;
    [ObservableProperty] private int selectedAccessIndex;
    [ObservableProperty] private int selectedStatusIndex;
    [ObservableProperty] private int selectedSortIndex;
    [ObservableProperty] private string headingText = "Khám phá thư viện";
    [ObservableProperty] private string countText = string.Empty;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private bool isRefreshing;

    public BrowseViewModel(IStoryService storyService)
    {
        _storyService = storyService;
    }

    // Đổi bất kỳ bộ lọc nào là tải lại ngay (web: onchange="this.form.submit()")
    partial void OnSelectedGenreIndexChanged(int value) => FilterChanged(value);
    partial void OnSelectedAccessIndexChanged(int value) => FilterChanged(value);
    partial void OnSelectedStatusIndexChanged(int value) => FilterChanged(value);
    partial void OnSelectedSortIndexChanged(int value) => FilterChanged(value);

    private void FilterChanged(int value)
    {
        if (_suppress || value < 0) return; // Picker tự gán -1 khi nạp lại danh sách, bỏ qua
        _ = ReloadAsync();
    }

    // Gọi mỗi lần tab hiện ra; chỉ tải lần đầu (kéo xuống để làm mới)
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_genresLoaded) return;

        await RunSafeAsync(async () =>
        {
            var genres = await _storyService.GetGenresAsync();

            _suppress = true;
            try
            {
                _genres = genres;
                GenreOptions.Clear();
                GenreOptions.Add("Tất cả thể loại");
                foreach (var g in genres) GenreOptions.Add(g.Name);

                // gán -1 rồi 0 để Picker chắc chắn hiển thị lại "Tất cả thể loại"
                SelectedGenreIndex = -1;
                SelectedGenreIndex = 0;
            }
            finally
            {
                _suppress = false;
            }

            _genresLoaded = true;
            await LoadStoriesAsync();
        });
    }

    [RelayCommand]
    private Task SearchAsync() => ReloadAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await ReloadAsync();
        IsRefreshing = false;
    }

    // Nút "Xóa lọc" của web
    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        _suppress = true;
        try
        {
            SearchText = string.Empty;
            SelectedGenreIndex = 0;
            SelectedAccessIndex = 0;
            SelectedStatusIndex = 0;
            SelectedSortIndex = 0;
        }
        finally
        {
            _suppress = false;
        }
        await ReloadAsync();
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

    private async Task ReloadAsync()
    {
        if (IsBusy) { _reloadQueued = true; return; }
        do
        {
            _reloadQueued = false;
            await RunSafeAsync(LoadStoriesAsync);
        } while (_reloadQueued);
    }

    private async Task LoadStoriesAsync()
    {
        var q = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

        int? genreId = SelectedGenreIndex > 0 && SelectedGenreIndex <= _genres.Count
            ? _genres[SelectedGenreIndex - 1].GenreId
            : null;

        string? access = SelectedAccessIndex switch { 1 => "Free", 2 => "Mixed", 3 => "Paid", _ => null };
        string? status = SelectedStatusIndex switch { 1 => "Ongoing", 2 => "Completed", 3 => "Paused", _ => null };
        string sort = SelectedSortIndex switch { 1 => "newest", 2 => "title", _ => "updated" };

        var page = await _storyService.GetListAsync(q, genreId, status, access, sort, 1, PageSize);

        Stories.Clear();
        foreach (var s in page.Items) Stories.Add(s);

        HeadingText = q is null ? "Khám phá thư viện" : $"Kết quả cho “{q}”";
        CountText = $"{Stories.Count} truyện";
        ShowEmpty = Stories.Count == 0;
    }
}

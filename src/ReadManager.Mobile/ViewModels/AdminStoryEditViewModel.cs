using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Thêm / sửa truyện + danh sách chương (Areas/Admin/Views/Stories/Edit.cshtml của web)
[QueryProperty(nameof(StoryId), "id")]
public partial class AdminStoryEditViewModel : AdminViewModelBase
{
    private static readonly string[] StatusKeys = { "Ongoing", "Completed", "Paused" };
    private static readonly string[] AccessKeys = { "Free", "Mixed", "Paid" };
    private static readonly string[] VisibilityKeys = { "Draft", "Public", "Hidden" };

    private readonly IAdminService _admin;

    // Chữ trong các ô chọn — giống web
    public string[] StatusOptions { get; } = { "Đang ra", "Hoàn thành", "Tạm ngừng" };
    public string[] AccessOptions { get; } = { "Miễn phí toàn bộ", "Kết hợp (một số chương đầu miễn phí)", "Trả phí" };
    public string[] VisibilityOptions { get; } = { "Nháp", "Công khai", "Ẩn" };

    public ObservableCollection<SelectableGenre> Genres { get; } = new();
    public ObservableCollection<AdminChapterRow> Chapters { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew))]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(SaveText))]
    private int storyId;

    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string author = string.Empty;
    [ObservableProperty] private string synopsis = string.Empty;
    [ObservableProperty] private string coverUrl = string.Empty;
    [ObservableProperty] private string priceText = string.Empty;
    [ObservableProperty] private int statusIndex;
    [ObservableProperty] private int accessIndex;
    [ObservableProperty] private int visibilityIndex = 1; // truyện mới mặc định "Công khai" như web

    [ObservableProperty] private bool hasGenres;
    [ObservableProperty] private bool hasNoGenres = true;
    [ObservableProperty] private string chaptersHeader = "Chương (0)";
    [ObservableProperty] private bool hasNoChapters = true;

    public bool IsNew => StoryId == 0;
    public bool IsEditing => StoryId != 0;
    public string PageTitle => IsNew ? "Thêm truyện" : "Sửa truyện";
    public string SaveText => IsNew ? "Tạo truyện" : "Lưu thay đổi";

    public AdminStoryEditViewModel(IAdminService admin, IAuthService auth) : base(auth)
    {
        _admin = admin;
    }

    // Lần đầu mở trang: nạp thể loại + (nếu là sửa) thông tin truyện và chương
    [RelayCommand]
    private Task LoadAsync() => RunSafeAsync(async () =>
    {
        if (!await EnsureAdminAsync()) return;

        var genres = await _admin.GetGenresAsync();
        var selected = new HashSet<int>();

        if (StoryId > 0)
        {
            var s = await _admin.GetStoryAsync(StoryId);
            Title = s.Title;
            Author = s.AuthorName;
            Synopsis = s.Synopsis;
            CoverUrl = s.CoverUrl ?? string.Empty;
            StatusIndex = Math.Max(0, Array.IndexOf(StatusKeys, s.PublicationStatus));
            AccessIndex = Math.Max(0, Array.IndexOf(AccessKeys, s.AccessPolicy));
            VisibilityIndex = Math.Max(0, Array.IndexOf(VisibilityKeys, s.Visibility));
            PriceText = (s.CurrentPrice ?? 0) > 0 ? ((long)s.CurrentPrice!.Value).ToString(CultureInfo.InvariantCulture) : string.Empty;
            foreach (var g in s.Genres) selected.Add(g.GenreId);
        }

        Genres.Clear();
        foreach (var g in genres)
            Genres.Add(new SelectableGenre { GenreId = g.GenreId, Name = g.Name, IsSelected = selected.Contains(g.GenreId) });
        HasGenres = Genres.Count > 0;
        HasNoGenres = !HasGenres;

        if (StoryId > 0) await LoadChaptersCoreAsync();
    });

    // Quay lại từ màn sửa chương / tải nhiều chương -> chỉ nạp lại danh sách chương
    [RelayCommand]
    private async Task ReloadChaptersAsync()
    {
        if (StoryId <= 0) return;
        await RunSafeAsync(LoadChaptersCoreAsync);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ToastMessage = null;
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Vui lòng nhập tên truyện.";
            return;
        }

        var access = AccessKeys[Math.Clamp(AccessIndex, 0, AccessKeys.Length - 1)];
        var visibility = VisibilityKeys[Math.Clamp(VisibilityIndex, 0, VisibilityKeys.Length - 1)];

        // Giống web: truyện miễn phí hoặc giá <= 0 thì không gửi giá
        decimal? price = null;
        var digits = (PriceText ?? string.Empty).Replace(".", "").Replace(",", "").Trim();
        if (access != "Free" && decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0)
            price = p;

        var cover = string.IsNullOrWhiteSpace(CoverUrl) ? null : CoverUrl.Trim();
        var genreIds = Genres.Where(g => g.IsSelected).Select(g => g.GenreId).ToList();

        await RunSafeAsync(async () =>
        {
            if (StoryId == 0)
            {
                var id = await _admin.CreateStoryAsync(new CreateStoryRequest
                {
                    Title = Title.Trim(),
                    AuthorName = Author ?? string.Empty,
                    Synopsis = Synopsis ?? string.Empty,
                    CoverUrl = cover,
                    AccessPolicy = access,
                    Visibility = visibility,
                    CurrentPrice = price,
                    GenreIds = genreIds,
                    CreatedBy = Auth.CurrentUser?.UserId ?? 0
                });
                StoryId = id; // chuyển sang chế độ "Sửa truyện" để thêm chương
                ToastMessage = "Đã lưu truyện.";
                await LoadChaptersCoreAsync();
            }
            else
            {
                await _admin.UpdateStoryAsync(StoryId, new UpdateStoryRequest
                {
                    Title = Title.Trim(),
                    AuthorName = Author ?? string.Empty,
                    Synopsis = Synopsis ?? string.Empty,
                    CoverUrl = cover,
                    PublicationStatus = StatusKeys[Math.Clamp(StatusIndex, 0, StatusKeys.Length - 1)],
                    AccessPolicy = access,
                    Visibility = visibility,
                    CurrentPrice = price,
                    GenreIds = genreIds
                });
                ToastMessage = "Đã lưu truyện.";
            }
        });
    }

    [RelayCommand]
    private async Task AddChapterAsync()
    {
        if (StoryId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.AdminChapterEditPage), new Dictionary<string, object>
        {
            { "storyId", StoryId },
            { "no", 0 }
        });
    }

    [RelayCommand]
    private async Task EditChapterAsync(AdminChapterRow? chapter)
    {
        if (chapter is null) return;
        await Shell.Current.GoToAsync(nameof(Views.AdminChapterEditPage), new Dictionary<string, object>
        {
            { "storyId", StoryId },
            { "no", chapter.ChapterNumber }
        });
    }

    [RelayCommand]
    private async Task DeleteChapterAsync(AdminChapterRow? chapter)
    {
        if (chapter is null) return;
        ToastMessage = null;

        var ok = await Shell.Current.DisplayAlert("Xóa chương", $"Xóa chương {chapter.ChapterNumber}?", "Xóa", "Hủy");
        if (!ok) return;

        await RunSafeAsync(async () =>
        {
            await _admin.DeleteChapterAsync(chapter.ChapterId);
            ToastMessage = "Đã xóa chương.";
            await LoadChaptersCoreAsync();
        });
    }

    [RelayCommand]
    private async Task BulkImportAsync()
    {
        if (StoryId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.AdminBulkImportPage), new Dictionary<string, object>
        {
            { "storyId", StoryId },
            { "storyTitle", Title ?? string.Empty }
        });
    }

    private async Task LoadChaptersCoreAsync()
    {
        var list = await _admin.GetChaptersAsync(StoryId);
        Chapters.Clear();
        foreach (var c in list.OrderBy(c => c.ChapterNumber)) Chapters.Add(c);
        ChaptersHeader = $"Chương ({Chapters.Count})";
        HasNoChapters = Chapters.Count == 0;
    }
}

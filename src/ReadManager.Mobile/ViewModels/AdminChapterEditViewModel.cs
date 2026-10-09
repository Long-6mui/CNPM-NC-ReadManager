using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Thêm / sửa 1 chương (Areas/Admin/Views/Stories/ChapterEdit.cshtml của web)
[QueryProperty(nameof(StoryId), "storyId")]
[QueryProperty(nameof(ChapterNo), "no")]
public partial class AdminChapterEditViewModel : AdminViewModelBase
{
    private readonly IAdminService _admin;
    private bool _loaded;

    public string[] StatusOptions { get; } =
    {
        "Nháp — chưa duyệt, độc giả không thấy",
        "Công khai — độc giả đọc được"
    };

    [ObservableProperty] private int storyId;
    [ObservableProperty] private int chapterNo;      // 0 = chương mới

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private int chapterId;

    [ObservableProperty] private string storyTitle = string.Empty;
    [ObservableProperty] private string numberText = string.Empty;
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string content = string.Empty;
    [ObservableProperty] private bool isFree;
    [ObservableProperty] private int statusIndex = 1; // mặc định "Công khai" như web

    public string PageTitle => ChapterId == 0 ? "Thêm chương" : "Sửa chương";

    public AdminChapterEditViewModel(IAdminService admin, IAuthService auth) : base(auth)
    {
        _admin = admin;
    }

    [RelayCommand]
    private Task LoadAsync() => RunSafeAsync(async () =>
    {
        if (_loaded) return;
        if (!await EnsureAdminAsync()) return;

        var story = await _admin.GetStoryAsync(StoryId);
        StoryTitle = story.Title;

        if (ChapterNo > 0)
        {
            // Sửa chương: đổ nội dung chương cũ vào form
            var c = await _admin.GetChapterAsync(StoryId, ChapterNo);
            ChapterId = c.ChapterId;
            NumberText = c.ChapterNumber.ToString(CultureInfo.InvariantCulture);
            Title = c.Title;
            Content = c.Content;
            IsFree = c.AccessLevel == "Free";
            StatusIndex = c.PublicationStatus == "Published" ? 1 : 0;
        }
        else
        {
            // Chương mới: gợi ý số chương tiếp theo (số lớn nhất + 1)
            var list = await _admin.GetChaptersAsync(StoryId);
            ChapterId = 0;
            NumberText = (list.Count == 0 ? 1 : list.Max(c => c.ChapterNumber) + 1).ToString(CultureInfo.InvariantCulture);
            IsFree = story.AccessPolicy == "Free";
            StatusIndex = 1;
        }
        _loaded = true;
    });

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!int.TryParse((NumberText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            || number < 1 || number > 100000)
        {
            ErrorMessage = "Số chương phải từ 1 trở lên.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Vui lòng nhập tiêu đề chương.";
            return;
        }
        if (Title.Trim().Length > 255)
        {
            ErrorMessage = "Tiêu đề tối đa 255 ký tự.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Content))
        {
            ErrorMessage = "Vui lòng nhập nội dung chương.";
            return;
        }
        if (Content.Length > 200000)
        {
            ErrorMessage = "Nội dung tối đa 200.000 ký tự.";
            return;
        }

        var body = new ChapterRequest
        {
            ChapterNumber = number,
            Title = Title.Trim(),
            Content = Content,
            AccessLevel = IsFree ? "Free" : "Paid",
            PublicationStatus = StatusIndex == 1 ? "Published" : "Draft"
        };

        await RunSafeAsync(async () =>
        {
            if (ChapterId == 0) await _admin.CreateChapterAsync(StoryId, body);
            else await _admin.UpdateChapterAsync(ChapterId, body);

            // Lưu xong quay lại trang sửa truyện (trang đó tự nạp lại danh sách chương)
            await Shell.Current.GoToAsync("..");
        });
    }
}

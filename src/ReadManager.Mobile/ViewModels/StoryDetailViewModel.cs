using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// Chi tiết truyện: API chỉ có GET api/stories/{id} nên điều hướng truyền "id" (không dùng slug).
[QueryProperty(nameof(StoryId), "id")]
public partial class StoryDetailViewModel : BaseViewModel
{
    private readonly IStoryService _storyService;
    private readonly ILibraryService _library;

    [ObservableProperty]
    private int storyId;

    [ObservableProperty]
    private StoryDetail? story;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteText))]
    private bool isFavorite;

    public string FavoriteText => IsFavorite ? "♥ Đã yêu thích" : "♡ Yêu thích";

    public StoryDetailViewModel(IStoryService storyService, ILibraryService library)
    {
        _storyService = storyService;
        _library = library;
    }

    // Shell tự gán StoryId khi điều hướng tới trang này
    partial void OnStoryIdChanged(int value)
    {
        if (value <= 0) return;
        _ = RunSafeAsync(async () =>
        {
            Story = await _storyService.GetStoryByIdAsync(value);

            // Ghi vào "Truyện đã xem gần đây" của Tủ sách
            _library.AddToHistory(Story);
            IsFavorite = _library.IsFavorite(Story.StoryId);
        });
    }

    [RelayCommand]
    private async Task OpenChaptersAsync()
    {
        if (StoryId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.ChapterListPage), new Dictionary<string, object>
        { ["storyId"] = StoryId, ["storyTitle"] = Story?.Title ?? "Danh sách chương" });
    }

    // Nút "Yêu thích" -> thêm/bỏ khỏi Tủ sách
    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Story is null) return;
        IsFavorite = _library.ToggleFavorite(Story);
    }
}

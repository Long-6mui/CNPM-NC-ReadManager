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

    [ObservableProperty]
    private int storyId;

    [ObservableProperty]
    private StoryDetail? story;

    public StoryDetailViewModel(IStoryService storyService)
    {
        _storyService = storyService;
    }

    // Shell tự gán StoryId khi điều hướng tới trang này
    partial void OnStoryIdChanged(int value)
    {
        if (value <= 0) return;
        _ = RunSafeAsync(async () =>
        {
            Story = await _storyService.GetStoryByIdAsync(value);
        });
    }
    [RelayCommand]
    private async Task OpenChaptersAsync()
    {
        if (StoryId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.ChapterListPage), new Dictionary<string, object>
        { ["storyId"] = StoryId, ["storyTitle"] = Story?.Title ?? "Danh sách chương" });
    }
}

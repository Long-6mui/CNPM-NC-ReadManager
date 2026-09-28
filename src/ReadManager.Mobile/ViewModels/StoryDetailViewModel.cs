using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

[QueryProperty(nameof(Slug), "slug")]
public partial class StoryDetailViewModel : BaseViewModel
{
    private readonly IStoryService _storyService;

    [ObservableProperty]
    private string slug = string.Empty;

    [ObservableProperty]
    private StoryDetail? story;

    public StoryDetailViewModel(IStoryService storyService)
    {
        _storyService = storyService;
    }

    // Được Shell tự gọi lại khi Slug đổi (mỗi lần điều hướng tới trang này với slug mới)
    partial void OnSlugChanged(string value)
    {
        _ = RunSafeAsync(async () =>
        {
            Story = await _storyService.GetStoryDetailAsync(value);
        });
    }

    [RelayCommand]
    private async Task GoToChaptersAsync()
    {
        if (Story is null) return;
        await Shell.Current.GoToAsync(nameof(Views.ChapterListPage), new Dictionary<string, object>
        {
            { "storyId", Story.StoryId },
            { "storyTitle", Story.Title }
        });
    }
}

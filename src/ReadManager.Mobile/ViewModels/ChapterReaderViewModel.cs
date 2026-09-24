using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

[QueryProperty(nameof(ChapterId), "chapterId")]
public partial class ChapterReaderViewModel : BaseViewModel
{
    private readonly IChapterService _chapterService;

    [ObservableProperty]
    private int chapterId;

    [ObservableProperty]
    private ChapterContent? chapter;

    public ChapterReaderViewModel(IChapterService chapterService)
    {
        _chapterService = chapterService;
    }

    partial void OnChapterIdChanged(int value)
    {
        _ = RunSafeAsync(async () =>
        {
            Chapter = await _chapterService.GetChapterContentAsync(value);
        });
    }

    [RelayCommand]
    private async Task GoToPreviousAsync()
    {
        if (Chapter?.PreviousChapterId is int prevId)
            await NavigateToChapterAsync(prevId);
    }

    [RelayCommand]
    private async Task GoToNextAsync()
    {
        if (Chapter?.NextChapterId is int nextId)
            await NavigateToChapterAsync(nextId);
    }

    private async Task NavigateToChapterAsync(int targetChapterId)
    {
        // Điều hướng lại chính trang này với chapterId mới để tải nội dung chương khác
        await Shell.Current.GoToAsync($"{nameof(Views.ChapterReaderPage)}?chapterId={targetChapterId}");
    }
}

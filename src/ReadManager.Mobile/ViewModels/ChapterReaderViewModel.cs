using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

[QueryProperty(nameof(ChapterId), "chapterId")]
public partial class ChapterReaderViewModel : BaseViewModel
{
    private readonly IChapterService _chapterService;
    private readonly ILibraryService _library;

    [ObservableProperty]
    private int chapterId;

    [ObservableProperty]
    private ChapterContent? chapter;

    public ChapterReaderViewModel(IChapterService chapterService, ILibraryService library)
    {
        _chapterService = chapterService;
        _library = library;
    }

    partial void OnChapterIdChanged(int value)
    {
        _ = RunSafeAsync(async () =>
        {
            Chapter = await _chapterService.GetChapterContentAsync(value);
            if (Chapter.IsLocked) ErrorMessage = "Chương trả phí chưa được mở khóa.";
            else _library.RecordChapter(Chapter.StoryId, Chapter.ChapterId, Chapter.ChapterNumber, Chapter.Title); // Tủ sách: nhớ chương đang đọc
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

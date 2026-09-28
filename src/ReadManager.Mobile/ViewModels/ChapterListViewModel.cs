using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

[QueryProperty(nameof(StoryId), "storyId")]
[QueryProperty(nameof(StoryTitle), "storyTitle")]
public partial class ChapterListViewModel : BaseViewModel
{
    private readonly IChapterService _chapterService;

    [ObservableProperty]
    private int storyId;

    [ObservableProperty]
    private string storyTitle = string.Empty;

    public ObservableCollection<ChapterSummary> Chapters { get; } = new();

    public ChapterListViewModel(IChapterService chapterService)
    {
        _chapterService = chapterService;
    }

    partial void OnStoryIdChanged(int value)
    {
        _ = RunSafeAsync(async () =>
        {
            var chapters = await _chapterService.GetChaptersAsync(value);
            Chapters.Clear();
            // Sắp theo số chương tăng dần để đọc từ đầu
            foreach (var c in chapters.OrderBy(c => c.ChapterNumber))
                Chapters.Add(c);
        });
    }

    [RelayCommand]
    private async Task OpenChapterAsync(ChapterSummary? chapter)
    {
        if (chapter is null) return;

        if (chapter.AccessLevel != "Free")
        {
            ErrorMessage = "Chương này chưa mở khoá miễn phí.";
            return;
        }

        await Shell.Current.GoToAsync(nameof(Views.ChapterReaderPage), new Dictionary<string, object>
        {
            { "chapterId", chapter.ChapterId }
        });
    }
}

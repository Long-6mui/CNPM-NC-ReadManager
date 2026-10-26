using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// "Đang đọc / Đọc tiếp". Hiện lưu trên máy (LibraryService); khi Backend 2 có API ReadingProgress thì đổi sang ApiReadingProgressService.
public interface IReadingProgressService
{
    Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle);
    Task<List<LibraryEntry>> GetReadingAsync();
}

public class LocalReadingProgressService : IReadingProgressService
{
    private readonly ILibraryService _library;

    public LocalReadingProgressService(ILibraryService library) => _library = library;

    public Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle)
    {
        _library.RecordChapter(storyId, chapterId, chapterNumber, chapterTitle);
        return Task.CompletedTask;
    }

    public Task<List<LibraryEntry>> GetReadingAsync()
        => Task.FromResult(_library.GetHistory()
            .Where(e => e.LastChapterId > 0)
            .OrderByDescending(e => e.ViewedAt)
            .ToList());
}

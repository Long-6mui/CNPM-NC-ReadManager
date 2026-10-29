using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Tiến độ đăng nhập được đồng bộ API; khách dùng bản lưu trên máy.
public interface IReadingProgressService
{
    Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle, decimal scrollPercent = 0);
    Task<ReadingProgressDto?> GetProgressAsync(int storyId);
    Task<List<LibraryEntry>> GetReadingAsync();
    Task RemoveAsync(int storyId);
}

public class LocalReadingProgressService : IReadingProgressService
{
    private readonly ILibraryService _library;

    public LocalReadingProgressService(ILibraryService library) => _library = library;

    public Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle, decimal scrollPercent = 0)
    {
        _library.RecordChapter(storyId, chapterId, chapterNumber, chapterTitle, scrollPercent);
        return Task.CompletedTask;
    }

    public Task<ReadingProgressDto?> GetProgressAsync(int storyId)
    {
        var e = _library.GetHistory().FirstOrDefault(e => e.StoryId == storyId && e.LastChapterId > 0);
        return Task.FromResult(e is null ? null : new ReadingProgressDto { StoryId = storyId,
            ChapterId = e.LastChapterId, ChapterNumber = e.LastChapterNumber, ChapterTitle = e.LastChapterTitle,
            ScrollPercent = e.ScrollPercent, UpdatedAt = e.ViewedAt });
    }
    public Task RemoveAsync(int storyId) { _library.RemoveFromHistory(storyId); return Task.CompletedTask; }

    public Task<List<LibraryEntry>> GetReadingAsync()
        => Task.FromResult(_library.GetHistory()
            .Where(e => e.LastChapterId > 0)
            .OrderByDescending(e => e.ViewedAt)
            .ToList());
}

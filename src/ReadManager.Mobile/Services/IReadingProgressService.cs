using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// "Đang đọc / Đọc tiếp".
//   - Khách (chưa đăng nhập) hoặc API tắt: lưu trên máy (LibraryService)
//   - Đã nối Backend 2: lưu thêm lên server (ApiReadingProgressService) để đồng bộ với Web
public interface IReadingProgressService
{
    // scrollPercent: vị trí cuộn trong chương, 0 = đầu chương, 100 = cuối chương
    Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle, decimal scrollPercent = 0);
    Task<List<LibraryEntry>> GetReadingAsync();
    Task RemoveAsync(int storyId);
}

public class LocalReadingProgressService : IReadingProgressService
{
    private readonly ILibraryService _library;

    public LocalReadingProgressService(ILibraryService library) => _library = library;

    public Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle, decimal scrollPercent = 0)
    {
        _library.RecordChapter(storyId, chapterId, chapterNumber, chapterTitle);
        return Task.CompletedTask;
    }

    public Task<List<LibraryEntry>> GetReadingAsync()
        => Task.FromResult(_library.GetHistory()
            .Where(e => e.LastChapterId > 0)
            .OrderByDescending(e => e.ViewedAt)
            .ToList());

    public Task RemoveAsync(int storyId)
    {
        _library.RemoveFromHistory(storyId);
        return Task.CompletedTask;
    }
}

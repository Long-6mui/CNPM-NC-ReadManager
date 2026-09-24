using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

public interface IChapterService
{
    Task<List<ChapterSummary>> GetChaptersAsync(int storyId);
    Task<ChapterContent> GetChapterContentAsync(int chapterId);
}

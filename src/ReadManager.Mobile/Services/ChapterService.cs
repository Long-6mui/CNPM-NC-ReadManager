using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

public class ChapterService : IChapterService
{
    private readonly ApiClient _api;

    // Routes khớp ChaptersController; quyền đọc do API quyết định qua IsLocked.
    public ChapterService(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<ChapterSummary>> GetChaptersAsync(int storyId)
    {
        var result = await _api.GetAsync<List<ChapterSummary>>($"stories/{storyId}/chapters");
        return result ?? new List<ChapterSummary>();
    }

    public async Task<ChapterContent> GetChapterContentAsync(int chapterId)
    {
        return await _api.GetAsync<ChapterContent>($"chapters/{chapterId}")
            ?? throw new ApiException(404, "Không tìm thấy chương.");
    }
}

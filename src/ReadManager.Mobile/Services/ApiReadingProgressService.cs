using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Bản nối API ReadingProgress của Backend 2 — ĐANG TẮT (ApiSettings.UseReadingProgressApi = false).
// Đường dẫn dưới đây là DỰ ĐOÁN, sửa cho khớp Backend 2. Lỗi mạng/404 thì luôn quay về dữ liệu trên máy.
public class ApiReadingProgressService : IReadingProgressService
{
    private const string ProgressUri = "reading-progress";

    private readonly ApiClient _api;
    private readonly LocalReadingProgressService _local;

    public ApiReadingProgressService(ApiClient api, LocalReadingProgressService local)
    {
        _api = api;
        _local = local;
    }

    public async Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle)
    {
        await _local.SaveAsync(storyId, chapterId, chapterNumber, chapterTitle); // luôn nhớ trên máy
        try
        {
            await _api.PutAsync<SaveReadingProgressRequest, object>(ProgressUri,
                new SaveReadingProgressRequest { StoryId = storyId, ChapterId = chapterId });
        }
        catch { /* chưa có API / mất mạng: bỏ qua, đã lưu trên máy */ }
    }

    public async Task<List<LibraryEntry>> GetReadingAsync()
    {
        try
        {
            var items = await _api.GetAsync<List<ReadingProgressDto>>(ProgressUri);
            if (items is { Count: > 0 })
            {
                return items.OrderByDescending(p => p.UpdatedAt).Select(p => new LibraryEntry
                {
                    StoryId = p.StoryId,
                    Title = p.StoryTitle,
                    AuthorName = p.AuthorName,
                    CoverUrl = p.CoverUrl,
                    ViewedAt = p.UpdatedAt,
                    LastChapterId = p.ChapterId,
                    LastChapterNumber = p.ChapterNumber,
                    LastChapterTitle = p.ChapterTitle,
                }).ToList();
            }
        }
        catch { /* dùng dữ liệu trên máy */ }
        return await _local.GetReadingAsync();
    }
}

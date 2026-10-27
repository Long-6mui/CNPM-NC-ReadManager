using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.Services;

// ============================================================================
// NỐI API TIẾN ĐỘ ĐỌC THẬT (Backend 2 — ReadingProgressController):
//   PUT    api/stories/{id}/progress   lưu điểm đọc tiếp
//   GET    api/me/reading              danh sách truyện đang đọc
//   DELETE api/stories/{id}/progress   bỏ khỏi danh sách đang đọc
// Luôn lưu cả trên máy; lỗi mạng / chưa đăng nhập thì chỉ dùng dữ liệu trên máy.
// ============================================================================
public class ApiReadingProgressService : IReadingProgressService
{
    private readonly ApiClient _api;
    private readonly LocalReadingProgressService _local;
    private readonly IAuthService _auth;

    public ApiReadingProgressService(ApiClient api, LocalReadingProgressService local, IAuthService auth)
    {
        _api = api;
        _local = local;
        _auth = auth;
    }

    public async Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle, decimal scrollPercent = 0)
    {
        await _local.SaveAsync(storyId, chapterId, chapterNumber, chapterTitle, scrollPercent);
        if (_auth.CurrentUser is null) return;   // khách: chỉ lưu trên máy

        try
        {
            var percent = Math.Clamp(scrollPercent, 0, 100);
            await _api.PutAsync<SaveReadingProgressRequest, object>($"stories/{storyId}/progress",
                new SaveReadingProgressRequest { ChapterId = chapterId, ScrollPercent = percent });
        }
        catch { /* mất mạng / chương chưa công khai: đã lưu trên máy */ }
    }

    public async Task<List<LibraryEntry>> GetReadingAsync()
    {
        if (_auth.CurrentUser is not null)
        {
            try
            {
                var page = await _api.GetAsync<PagedResult<ReadingStoryDto>>("me/reading?page=1&pageSize=50");
                if (page is not null)
                {
                    return page.Items.OrderByDescending(p => p.LastReadAt).Select(p => new LibraryEntry
                    {
                        StoryId = p.StoryId,
                        Title = p.Title,
                        AuthorName = p.AuthorName,
                        CoverUrl = p.CoverUrl,
                        PublishedChapterCount = p.PublishedChapterCount,
                        ViewedAt = p.LastReadAt,
                        LastChapterId = p.ChapterId,
                        LastChapterNumber = p.ChapterNumber,
                        LastChapterTitle = p.ChapterTitle,
                    }).ToList();
                }
            }
            catch { /* mất mạng: dùng dữ liệu trên máy */ }
        }
        return await _local.GetReadingAsync();
    }

    public async Task RemoveAsync(int storyId)
    {
        await _local.RemoveAsync(storyId);
        if (_auth.CurrentUser is null) return;
        try { await _api.DeleteAsync($"stories/{storyId}/progress"); }
        catch { /* 404 = đã không còn trong danh sách */ }
    }
}

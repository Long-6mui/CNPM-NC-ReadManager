using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

public class ChapterService : IChapterService
{
    private readonly ApiClient _api;

    // TODO: xác nhận lại route với Backend 4 (ChaptersController) —
    // đang giả định GET api/stories/{storyId}/chapters, GET api/chapters/{chapterId}
    // API cần tự chặn/permit theo AccessLevel; Sprint 1 mobile chỉ hiển thị nút "Đọc" cho chương Free,
    // chương Paid hiển thị khoá (chưa xử lý thanh toán ở sprint này).
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

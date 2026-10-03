using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public interface IStoryService
{
    Task<PagedResult<StorySummary>> GetStoriesAsync(int page = 1, string? search = null, int? genreId = null);
    Task<StoryDetail> GetStoryDetailAsync(string slug);
    Task<List<Genre>> GetGenresAsync();

    // Hai hàm dưới khớp đúng API thật (GET api/stories?q=&sort=&access=&pageSize=, GET api/stories/{id}).
    // Có sẵn thân hàm mặc định để các lớp Mock cũ không bị lỗi biên dịch.
    Task<PagedResult<StorySummary>> GetListAsync(string? search, int? genreId, string sort, string? access, int page, int pageSize)
        => GetStoriesAsync(page, search, genreId);

    // Bản đầy đủ có thêm "status" (Ongoing | Completed | Paused) — dùng ở tab Khám phá.
    Task<PagedResult<StorySummary>> GetListAsync(string? search, int? genreId, string? status, string? access, string sort, int page, int pageSize)
        => GetStoriesAsync(page, search, genreId);

    Task<StoryDetail> GetStoryByIdAsync(int id)
        => throw new NotSupportedException();
}

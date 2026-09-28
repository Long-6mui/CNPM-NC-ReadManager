using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public interface IStoryService
{
    Task<PagedResult<StorySummary>> GetStoriesAsync(int page = 1, string? search = null, int? genreId = null);
    Task<StoryDetail> GetStoryDetailAsync(string slug);
    Task<List<Genre>> GetGenresAsync();
}

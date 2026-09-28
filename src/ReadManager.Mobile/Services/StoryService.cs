using System.Web;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public class StoryService : IStoryService
{
    private readonly ApiClient _api;

    // TODO: xác nhận lại route với Backend 3 (StoriesController/GenresController) —
    // đang giả định GET api/stories?page=&search=&genreId=, GET api/stories/{slug}, GET api/genres
    public StoryService(ApiClient api)
    {
        _api = api;
    }

    public async Task<PagedResult<StorySummary>> GetStoriesAsync(int page = 1, string? search = null, int? genreId = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["page"] = page.ToString();
        if (!string.IsNullOrWhiteSpace(search)) query["search"] = search;
        if (genreId.HasValue) query["genreId"] = genreId.Value.ToString();

        var result = await _api.GetAsync<PagedResult<StorySummary>>($"stories?{query}");
        return result ?? new PagedResult<StorySummary>();
    }

    public async Task<StoryDetail> GetStoryDetailAsync(string slug)
    {
        return await _api.GetAsync<StoryDetail>($"stories/{slug}")
            ?? throw new ApiException(404, "Không tìm thấy truyện.");
    }

    public async Task<List<Genre>> GetGenresAsync()
    {
        var result = await _api.GetAsync<List<Genre>>("genres");
        return result ?? new List<Genre>();
    }
}

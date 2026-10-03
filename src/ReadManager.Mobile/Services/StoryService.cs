using System.Web;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public class StoryService : IStoryService
{
    private readonly ApiClient _api;

    // Route khớp API thật: GET api/stories, GET api/stories/{id}, GET api/genres
    public StoryService(ApiClient api)
    {
        _api = api;
    }

    // Giống ApiClient ở Web (StoriesApiClient.ListAsync): q, genreId, access, sort, page, pageSize
    public Task<PagedResult<StorySummary>> GetListAsync(
        string? search, int? genreId, string sort, string? access, int page, int pageSize)
        => GetListAsync(search, genreId, null, access, sort, page, pageSize);

    public async Task<PagedResult<StorySummary>> GetListAsync(
        string? search, int? genreId, string? status, string? access, string sort, int page, int pageSize)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["sort"] = string.IsNullOrWhiteSpace(sort) ? "updated" : sort;
        query["page"] = page.ToString();
        query["pageSize"] = pageSize.ToString();
        if (!string.IsNullOrWhiteSpace(search)) query["q"] = search;
        if (genreId.HasValue) query["genreId"] = genreId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(access)) query["access"] = access;
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;

        var result = await _api.GetAsync<PagedResult<StorySummary>>($"stories?{query}");
        return result ?? new PagedResult<StorySummary>();
    }

    public Task<PagedResult<StorySummary>> GetStoriesAsync(int page = 1, string? search = null, int? genreId = null)
        => GetListAsync(search, genreId, "updated", null, page, 12);

    public async Task<StoryDetail> GetStoryByIdAsync(int id)
    {
        return await _api.GetAsync<StoryDetail>($"stories/{id}")
            ?? throw new ApiException(404, "Không tìm thấy truyện.");
    }

    // API chưa có route theo slug — giữ lại cho đủ interface cũ, màn hình mới không dùng
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

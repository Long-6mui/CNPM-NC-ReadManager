using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

// Route khớp StoriesController / ChaptersController / GenresController của API.
public class AdminService : IAdminService
{
    private readonly ApiClient _api;

    public AdminService(ApiClient api)
    {
        _api = api;
    }

    // Giống web: lấy từng trang 50 truyện cho tới khi đủ
    public async Task<List<StorySummary>> GetAllStoriesAsync()
    {
        var all = new List<StorySummary>();
        var page = 1;
        while (true)
        {
            var result = await _api.GetAsync<PagedResult<StorySummary>>($"stories/admin?page={page}&pageSize=50")
                         ?? new PagedResult<StorySummary>();
            all.AddRange(result.Items);
            if (result.Items.Count == 0 || all.Count >= result.TotalItems) break;
            page++;
        }
        return all;
    }

    public async Task<StoryDetail> GetStoryAsync(int storyId)
        => await _api.GetAsync<StoryDetail>($"stories/{storyId}/admin")
           ?? throw new ApiException(404, "Không tìm thấy truyện.");

    public async Task<int> CreateStoryAsync(CreateStoryRequest request)
    {
        var created = await _api.PostAsync<CreateStoryRequest, StoryDetail>("stories", request)
                      ?? throw new ApiException(0, "Không nhận được phản hồi từ máy chủ.");
        return created.StoryId;
    }

    public async Task UpdateStoryAsync(int storyId, UpdateStoryRequest request)
        => await _api.PutAsync<UpdateStoryRequest, StoryDetail>($"stories/{storyId}", request);

    public async Task<List<AdminChapterRow>> GetChaptersAsync(int storyId)
    {
        try
        {
            return await _api.GetAsync<List<AdminChapterRow>>($"stories/{storyId}/chapters/admin")
                   ?? new List<AdminChapterRow>();
        }
        catch (ApiException ex) when (ex.StatusCode == 404) { return new List<AdminChapterRow>(); }
    }

    public async Task<AdminChapterDetail> GetChapterAsync(int storyId, int chapterNumber)
        => await _api.GetAsync<AdminChapterDetail>($"stories/{storyId}/chapters/{chapterNumber}")
           ?? throw new ApiException(404, "Không tìm thấy chương.");

    public async Task CreateChapterAsync(int storyId, ChapterRequest request)
        => await _api.PostAsync<ChapterRequest, AdminChapterRow>($"stories/{storyId}/chapters", request);

    public async Task UpdateChapterAsync(int chapterId, ChapterRequest request)
        => await _api.PutAsync<ChapterRequest, AdminChapterRow>($"chapters/{chapterId}", request);

    public Task DeleteChapterAsync(int chapterId) => _api.DeleteAsync($"chapters/{chapterId}");

    public async Task<List<Genre>> GetGenresAsync()
        => await _api.GetAsync<List<Genre>>("genres") ?? new List<Genre>();

    public Task CreateGenreAsync(string name) => _api.PostAsync("genres", new GenreRequest { Name = name });

    public Task DeleteGenreAsync(int genreId) => _api.DeleteAsync($"genres/{genreId}");

    public async Task<UploadFilesResult> UploadChapterFilesAsync(int storyId, MultipartFormDataContent form)
        => await _api.PostMultipartAsync<UploadFilesResult>($"stories/{storyId}/chapters/upload-files", form)
           ?? throw new ApiException(0, "Không nhận được phản hồi từ máy chủ.");
}

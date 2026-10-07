using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Các thao tác của trang quản trị (chỉ tài khoản Admin gọi được — API tự kiểm tra quyền).
public interface IAdminService
{
    Task<List<StorySummary>> GetAllStoriesAsync();
    Task<StoryDetail> GetStoryAsync(int storyId);
    Task<int> CreateStoryAsync(CreateStoryRequest request);          // trả về StoryId mới
    Task UpdateStoryAsync(int storyId, UpdateStoryRequest request);

    Task<List<AdminChapterRow>> GetChaptersAsync(int storyId);
    Task<AdminChapterDetail> GetChapterAsync(int storyId, int chapterNumber);
    Task CreateChapterAsync(int storyId, ChapterRequest request);
    Task UpdateChapterAsync(int chapterId, ChapterRequest request);
    Task DeleteChapterAsync(int chapterId);

    Task<List<Genre>> GetGenresAsync();
    Task CreateGenreAsync(string name);
    Task DeleteGenreAsync(int genreId);

    Task<UploadFilesResult> UploadChapterFilesAsync(int storyId, MultipartFormDataContent form);
}

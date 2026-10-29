using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;
namespace ReadManager.Mobile.Services;

public class ApiReadingProgressService(ApiClient api, LocalReadingProgressService local, ITokenStore tokens) : IReadingProgressService
{
    public async Task SaveAsync(int storyId, int chapterId, int chapterNumber, string chapterTitle, decimal scrollPercent = 0)
    {
        await local.SaveAsync(storyId, chapterId, chapterNumber, chapterTitle, scrollPercent);
        if (string.IsNullOrEmpty(await tokens.GetTokenAsync())) return;
        await api.PutAsync<SaveReadingProgressRequest, ReadingProgressDto>($"stories/{storyId}/progress",
            new() { ChapterId = chapterId, ScrollPercent = Math.Clamp(scrollPercent, 0, 100) });
    }
    public async Task<ReadingProgressDto?> GetProgressAsync(int storyId)
    {
        if (string.IsNullOrEmpty(await tokens.GetTokenAsync())) return await local.GetProgressAsync(storyId);
        try { return await api.GetAsync<ReadingProgressDto>($"stories/{storyId}/progress"); }
        catch (ApiException ex) when (ex.StatusCode == 404) { return null; }
    }
    public async Task RemoveAsync(int storyId)
    {
        if (!string.IsNullOrEmpty(await tokens.GetTokenAsync()))
        {
            try { await api.DeleteAsync($"stories/{storyId}/progress"); }
            catch (ApiException ex) when (ex.StatusCode == 404) { }
        }
        await local.RemoveAsync(storyId);
    }
    public async Task<List<LibraryEntry>> GetReadingAsync()
    {
        if (string.IsNullOrEmpty(await tokens.GetTokenAsync())) return await local.GetReadingAsync();
        var entries = new List<LibraryEntry>();
        for (var page = 1; ; page++)
        {
            var result = await api.GetAsync<PagedResult<ReadingStoryDto>>($"me/reading?page={page}&pageSize=50")
                ?? throw new ApiException(500, "API trả tiến độ trống.");
            entries.AddRange(result.Items.Select(p => new LibraryEntry
            {
                StoryId = p.StoryId, Title = p.Title, AuthorName = p.AuthorName, CoverUrl = p.CoverUrl,
                ViewedAt = p.LastReadAt, PublishedChapterCount = p.PublishedChapterCount,
                LastChapterId = p.ChapterId, LastChapterNumber = p.ChapterNumber, LastChapterTitle = p.ChapterTitle,
                ScrollPercent = p.ScrollPercent
            }));
            if (page >= result.TotalPages || result.Items.Count == 0) return entries;
        }
    }
}

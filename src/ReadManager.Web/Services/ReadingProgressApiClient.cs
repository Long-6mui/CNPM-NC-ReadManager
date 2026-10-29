using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
namespace ReadManager.Web.Services;

public record ReadingPositionApi(int StoryId, int ChapterId, int ChapterNumber, string ChapterTitle, decimal ScrollPercent, DateTime UpdatedAt);
public record ReadingStoryApi(int StoryId, string Title, string AuthorName, string? CoverUrl, int ChapterId,
    int ChapterNumber, string ChapterTitle, decimal ScrollPercent, int PublishedChapterCount, bool HasNewChapters, DateTime LastReadAt);

public class ReadingProgressApiClient(HttpClient http)
{
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await http.SendAsync(request);
    }
    public async Task<ReadingPositionApi?> GetAsync(int storyId, string? token)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/stories/{storyId}/progress", token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReadingPositionApi>();
    }
    public async Task<List<ReadingStoryApi>> ListAsync(string? token)
    {
        var items = new List<ReadingStoryApi>();
        for (var page=1; ; page++)
        {
            using var response = await SendAsync(HttpMethod.Get, $"api/me/reading?page={page}&pageSize=50", token);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<PagedApi<ReadingStoryApi>>()
                ?? throw new HttpRequestException("API trả danh sách tiến độ trống.");
            items.AddRange(data.Items);
            if (data.Items.Count == 0 || items.Count >= data.TotalCount) return items;
        }
    }
    public async Task SaveAsync(int storyId, int chapterId, decimal scrollPercent, string? token)
    {
        using var response = await SendAsync(HttpMethod.Put, $"api/stories/{storyId}/progress", token, new { chapterId, scrollPercent });
        response.EnsureSuccessStatusCode();
    }
    public async Task<bool> RemoveAsync(int storyId, string? token)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/stories/{storyId}/progress", token);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
    public static string ErrorMessage(Exception ex) => ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized }
        ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để đồng bộ tiến độ."
        : "Chưa kết nối được tiến độ đọc. Vui lòng thử lại; dữ liệu đã lưu trên máy chủ được giữ nguyên.";
}

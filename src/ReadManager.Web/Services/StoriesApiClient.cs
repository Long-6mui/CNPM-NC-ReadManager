using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Services;

public record GenreDto(int GenreId, string Name, string Slug, int StoryCount);
public record GenreRefApi(int GenreId, string Name);

public record StoryListItemApi(
    int StoryId, string Title, string Slug, string AuthorName, string? CoverUrl,
    string PublicationStatus, string AccessPolicy, decimal? CurrentPrice,
    int PublishedChapterCount, List<string> Genres, DateTime CreatedAt, DateTime UpdatedAt);

public record StoryDetailApi(
    int StoryId, string Title, string Slug, string AuthorName, string Synopsis, string? CoverUrl,
    string PublicationStatus, string Visibility, string AccessPolicy, decimal? CurrentPrice,
    int CreatedBy, int PublishedChapterCount, int FreeChapterCount, List<GenreRefApi> Genres,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? FirstPublishedAt);

public record PagedApi<T>(List<T> Items, int TotalCount, int Page, int PageSize);
public record ApiError(string? Message);
public record ApiResult(bool Ok, string Message, int? Id = null);

// ----- Chương (Backend 4) — các record này khớp với DTO bên API -----
// ChapterListItemApi  ↔ ChapterListItemDto   (1 dòng trong danh sách chương)
// ChapterReadApi      ↔ ChapterReadDto       (màn hình đọc chương)
// UploadChaptersResultApi ↔ UploadChaptersResultDto (kết quả tải nhiều chương)
public record ChapterListItemApi(
    int ChapterId, int StoryId, int ChapterNumber, string Title, string AccessLevel,
    string PublicationStatus, bool IsLocked, DateTime? PublishedAt, DateTime CreatedAt, DateTime UpdatedAt);

public record ChapterReadApi(
    int ChapterId, int StoryId, string StoryTitle, int ChapterNumber, string Title, string Content,
    string AccessLevel, string PublicationStatus, bool IsLocked,
    int? PreviousChapterId, int? NextChapterId, int? PreviousChapterNumber, int? NextChapterNumber,
    DateTime? PublishedAt, DateTime UpdatedAt);

public record UploadChaptersResultApi(int Created, int Updated, List<int> SkippedNumbers, string Message);

public class StoriesApiClient(HttpClient http)
{
    public async Task<PagedApi<StoryListItemApi>> AdminListAsync(string? token, int page = 1)
    {
        using var req = Build(HttpMethod.Get, $"api/stories/admin?page={page}&pageSize=50", token);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PagedApi<StoryListItemApi>>())!;
    }

    // ---------- Phần công khai (ai cũng xem được) ----------
    public async Task<PagedApi<StoryListItemApi>> ListAsync(
        string? q, int? genreId, string? status, string? access,
        string sort = "updated", int page = 1, int pageSize = 12)
    {
        var url = $"api/stories?sort={sort}&page={page}&pageSize={pageSize}"
            + (string.IsNullOrWhiteSpace(q) ? "" : $"&q={Uri.EscapeDataString(q)}")
            + (genreId is null ? "" : $"&genreId={genreId}")
            + (status is null ? "" : $"&status={status}")
            + (access is null ? "" : $"&access={access}");
        var result = await http.GetFromJsonAsync<PagedApi<StoryListItemApi>>(url);
        return result ?? new PagedApi<StoryListItemApi>([], 0, page, pageSize);
    }

    public async Task<List<GenreDto>> GenresAsync()
        => await http.GetFromJsonAsync<List<GenreDto>>("api/genres") ?? [];

    // Trả về null nếu truyện không tồn tại (hoặc chưa công khai)
    public async Task<StoryDetailApi?> GetStoryAsync(int id)
    {
        using var res = await http.GetAsync($"api/stories/{id}");
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<StoryDetailApi>();
    }

    // ---------- Phần quản trị (gửi kèm token của admin) ----------
    public async Task<StoryDetailApi?> GetStoryForAdminAsync(int id, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/stories/{id}/admin", token);
        using var res = await http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<StoryDetailApi>();
    }

    // id = 0 nghĩa là tạo mới (POST), id > 0 là cập nhật (PUT)
    public async Task<ApiResult> SaveStoryAsync(int id, object body, string? token)
    {
        var isNew = id == 0;
        using var req = Build(isNew ? HttpMethod.Post : HttpMethod.Put,
            isNew ? "api/stories" : $"api/stories/{id}", token, body);
        using var res = await http.SendAsync(req);
        var result = await ToResultAsync(res, "Đã lưu truyện.");
        if (!result.Ok) return result;
        var detail = await res.Content.ReadFromJsonAsync<StoryDetailApi>();
        return result with { Id = detail?.StoryId ?? id };
    }

    public async Task<ApiResult> CreateGenreAsync(string name, string? token)
    {
        using var req = Build(HttpMethod.Post, "api/genres", token, new { name });
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, "Đã thêm thể loại.");
    }

    public async Task<ApiResult> DeleteGenreAsync(int id, string? token)
    {
        using var req = Build(HttpMethod.Delete, $"api/genres/{id}", token);
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, "Đã xóa thể loại.");
    }

    // ---------- Chương (Backend 4) ----------
    // Danh sách chương đã công khai cho độc giả. Truyện không tồn tại / chưa công khai → rỗng.
    public async Task<List<ChapterListItemApi>> ChaptersAsync(int storyId)
    {
        using var res = await http.GetAsync($"api/stories/{storyId}/chapters");
        if (res.StatusCode == HttpStatusCode.NotFound) return [];
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<ChapterListItemApi>>() ?? [];
    }

    // Mọi chương, kể cả nháp — cho trang quản trị.
    public async Task<List<ChapterListItemApi>> ChaptersForAdminAsync(int storyId, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/stories/{storyId}/chapters/admin", token);
        using var res = await http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.NotFound) return [];
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<ChapterListItemApi>>() ?? [];
    }

    // Đọc 1 chương theo số. Gửi kèm token admin thì xem được cả chương nháp/trả phí.
    public async Task<ChapterReadApi?> GetChapterAsync(int storyId, int chapterNumber, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/stories/{storyId}/chapters/{chapterNumber}", token);
        using var res = await http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<ChapterReadApi>();
    }

    // chapterId = 0 nghĩa là tạo mới (POST), > 0 là cập nhật (PUT)
    public async Task<ApiResult> SaveChapterAsync(int storyId, int chapterId, object body, string? token)
    {
        var isNew = chapterId == 0;
        using var req = Build(isNew ? HttpMethod.Post : HttpMethod.Put,
            isNew ? $"api/stories/{storyId}/chapters" : $"api/chapters/{chapterId}", token, body);
        using var res = await http.SendAsync(req);
        var result = await ToResultAsync(res, "Đã lưu chương.");
        if (!result.Ok) return result;
        var saved = await res.Content.ReadFromJsonAsync<ChapterListItemApi>();
        return result with { Id = saved?.ChapterId ?? chapterId };
    }

    public async Task<ApiResult> DeleteChapterAsync(int chapterId, string? token)
    {
        using var req = Build(HttpMethod.Delete, $"api/chapters/{chapterId}", token);
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, "Đã xóa chương.");
    }

    public async Task<ApiResult> UploadChaptersAsync(int storyId, object body, string? token)
    {
        using var req = Build(HttpMethod.Post, $"api/stories/{storyId}/chapters/upload", token, body);
        using var res = await http.SendAsync(req);
        var result = await ToResultAsync(res, "Đã lưu các chương.");
        if (!result.Ok) return result;
        var bulk = await res.Content.ReadFromJsonAsync<UploadChaptersResultApi>();
        return string.IsNullOrWhiteSpace(bulk?.Message) ? result : result with { Message = bulk.Message };
    }

    // Tải chương bằng file .txt/.zip. Trả nguyên JSON của API để trang web tự vẽ bảng xem trước.
    // StatusCode khác 200 → trang web hiện lỗi theo mã (hết phiên, không có quyền...).
    public async Task<(HttpStatusCode Status, string Body)> UploadChapterFilesAsync(
        int storyId, MultipartFormDataContent form, string? token)
    {
        using var req = Build(HttpMethod.Post, $"api/stories/{storyId}/chapters/upload-files", token);
        req.Content = form;
        using var res = await http.SendAsync(req);
        return (res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    // ---------- Hàm phụ ----------
    private static HttpRequestMessage Build(HttpMethod method, string url, string? token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private static async Task<ApiResult> ToResultAsync(HttpResponseMessage res, string okMessage)
    {
        if (res.IsSuccessStatusCode) return new ApiResult(true, okMessage);
        string? message = null;
        try { message = (await res.Content.ReadFromJsonAsync<ApiError>())?.Message; } catch { }
        if (!string.IsNullOrWhiteSpace(message)) return new ApiResult(false, message);
        return new ApiResult(false, res.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Phiên đăng nhập hết hạn. Hãy đăng nhập lại.",
            HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            HttpStatusCode.NotFound => "Không tìm thấy dữ liệu.",
            HttpStatusCode.BadRequest => "Dữ liệu không hợp lệ.",
            _ => $"API trả về lỗi {(int)res.StatusCode}."
        });
    }

    // ---------- Đổi dữ liệu API sang dữ liệu giao diện ----------
    public static StoryCardVm ToCard(StoryListItemApi s) => new()
    {
        Id = s.StoryId,
        Title = s.Title,
        Author = s.AuthorName,
        CoverUrl = s.CoverUrl,
        Access = Enum.TryParse<AccessPolicy>(s.AccessPolicy, out var a) ? a : AccessPolicy.Free,
        Status = Enum.TryParse<StoryStatus>(s.PublicationStatus, out var st) ? st : StoryStatus.Ongoing,
        PublishedChapterCount = s.PublishedChapterCount,
        CreatedAtSort = s.CreatedAt,
        UpdatedAtSort = s.UpdatedAt
    };

    public static Story ToListStory(StoryListItemApi s) => new()
    {
        Id = s.StoryId,
        Title = s.Title,
        Author = s.AuthorName,
        CoverUrl = s.CoverUrl,
        Access = Enum.TryParse<AccessPolicy>(s.AccessPolicy, out var a) ? a : AccessPolicy.Free,
        Status = Enum.TryParse<StoryStatus>(s.PublicationStatus, out var st) ? st : StoryStatus.Ongoing,
        Price = s.CurrentPrice ?? 0,
        UpdatedAt = s.UpdatedAt
    };

    // Đổi 1 chương từ API sang dạng trang quản trị đang dùng
    public static Chapter ToChapter(ChapterListItemApi c) => new()
    {
        Id = c.ChapterId,
        StoryId = c.StoryId,
        No = c.ChapterNumber,
        Title = c.Title,
        IsFree = c.AccessLevel == "Free",
        Status = c.PublicationStatus == "Published" ? ChapterStatus.Reviewed : ChapterStatus.Draft,
        CreatedAt = c.PublishedAt ?? c.CreatedAt
    };

    public static Story ToStory(StoryDetailApi d) => new()
    {
        Id = d.StoryId,
        Title = d.Title,
        Author = d.AuthorName,
        Description = d.Synopsis,
        CoverUrl = d.CoverUrl,
        Access = Enum.TryParse<AccessPolicy>(d.AccessPolicy, out var a) ? a : AccessPolicy.Free,
        Status = Enum.TryParse<StoryStatus>(d.PublicationStatus, out var st) ? st : StoryStatus.Ongoing,
        Price = d.CurrentPrice ?? 0,
        FreeChapterCount = d.FreeChapterCount,
        UpdatedAt = d.UpdatedAt
    };
}
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
    string PublicationStatus, bool IsLocked, DateTime? PublishedAt, DateTime CreatedAt, DateTime UpdatedAt,
    DateTime? ScheduledAt = null, bool IsUpcoming = false);   // hẹn giờ ra mắt (giờ UTC)

public record ChapterReadApi(
    int ChapterId, int StoryId, string StoryTitle, int ChapterNumber, string Title, string Content,
    string AccessLevel, string PublicationStatus, bool IsLocked,
    int? PreviousChapterId, int? NextChapterId, int? PreviousChapterNumber, int? NextChapterNumber,
    DateTime? PublishedAt, DateTime UpdatedAt,
    DateTime? ScheduledAt = null, bool IsUpcoming = false);   // hẹn giờ ra mắt (giờ UTC)

// ----- Tương tác (Backend 4 — PB31–33) — các record này khớp với DTO bên API -----
// CommentApi        ↔ CommentDto          (1 bình luận)
// ReportApi         ↔ ReportDto           (1 báo cáo vi phạm — trang Admin)
// FollowStatusApi   ↔ FollowStatusDto     (nút Theo dõi)
// NotificationApi   ↔ NotificationDto     (1 thông báo chương mới)
public record CommentApi(
    int CommentId, int StoryId, int? ChapterId, int? ChapterNumber,
    int UserId, string DisplayName, string? AvatarUrl,
    string Content, DateTime CreatedAt, bool CanDelete);

public record ReportApi(
    int ReportId, int ReporterId, string ReporterName,
    string TargetType, int TargetId, int? StoryId, int? ChapterNumber,
    string TargetPreview, bool TargetExists,
    string Reason, string? Note, string Status, DateTime CreatedAt, DateTime? HandledAt);

public record FollowStatusApi(int StoryId, bool IsFollowing, int FollowerCount);
public record FollowedStoryApi(int StoryId, string Title, string? CoverUrl, DateTime FollowedAt);

public record NotificationApi(
    int NotificationId, int StoryId, int? ChapterId, int? ChapterNumber,
    string Message, bool IsRead, DateTime CreatedAt);

public record NotificationListApi(List<NotificationApi> Items, int TotalCount, int UnreadCount, int Page, int PageSize);

// ----- Đơn hàng (Backend 3 — PB19, PB21, PB26) — khớp OrderQuoteDto / OrderDto bên API -----
public record OrderQuoteApi(
    string OrderType, int? StoryId, int? PlanId, int? PlanVersionId,
    string ItemName, decimal Amount, int? DurationMonths,
    bool CanBuy, bool AlreadyOwned, int? PendingOrderId, string? Message);

public record OrderApi(
    int OrderId, int UserId, string? UserName, string OrderType,
    int? PlanId, int? PlanVersionId, int? StoryId,
    string ItemName, decimal Amount, int? DurationMonths,
    string Status, DateTime CreatedAt, DateTime? PaidAt, DateTime? ExpiresAt,
    bool CanPay);

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
    // ---------- PB32 — Bình luận ----------
    // Danh sách bình luận của 1 chương (mới nhất ở trên). Gửi token để API biết bình luận nào mình được xóa.
    public async Task<PagedApi<CommentApi>> CommentsAsync(int storyId, int chapterId, string? token, int page = 1)
    {
        using var req = Build(HttpMethod.Get, $"api/stories/{storyId}/comments?chapterId={chapterId}&page={page}&pageSize=50", token);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return new PagedApi<CommentApi>([], 0, page, 50);
        return await res.Content.ReadFromJsonAsync<PagedApi<CommentApi>>() ?? new PagedApi<CommentApi>([], 0, page, 50);
    }

    public async Task<ApiResult> PostCommentAsync(int storyId, int chapterId, string content, string? token)
    {
        using var req = Build(HttpMethod.Post, $"api/stories/{storyId}/comments", token, new { chapterId, content });
        using var res = await http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.TooManyRequests)
            return new ApiResult(false, "Bạn thao tác nhanh quá. Vui lòng chờ 1 phút.");
        return await ToResultAsync(res, "Đã đăng bình luận.");
    }

    public async Task<ApiResult> DeleteCommentAsync(int commentId, string? token)
    {
        using var req = Build(HttpMethod.Delete, $"api/comments/{commentId}", token);
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, "Đã xóa bình luận.");
    }

    // ---------- PB33 — Báo cáo vi phạm ----------
    public async Task<List<string>> ReportReasonsAsync()
    {
        using var res = await http.GetAsync("api/reports/reasons");
        if (!res.IsSuccessStatusCode) return [];
        return await res.Content.ReadFromJsonAsync<List<string>>() ?? [];
    }

    // targetType: "Story" | "Chapter" | "Comment"
    public async Task<ApiResult> CreateReportAsync(string targetType, int targetId, string reason, string? note, string? token)
    {
        using var req = Build(HttpMethod.Post, "api/reports", token, new { targetType, targetId, reason, note });
        using var res = await http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.TooManyRequests)
            return new ApiResult(false, "Bạn thao tác nhanh quá. Vui lòng chờ 1 phút.");
        return await ToResultAsync(res, "Đã gửi báo cáo. Cảm ơn bạn, Admin sẽ xem xét sớm.");
    }

    // Admin: status = "Pending" | "Resolved" | "Rejected" | null (tất cả)
    public async Task<PagedApi<ReportApi>> AdminReportsAsync(string? status, int page, string? token)
    {
        var url = $"api/admin/reports?page={page}&pageSize=20" + (string.IsNullOrEmpty(status) ? "" : $"&status={status}");
        using var req = Build(HttpMethod.Get, url, token);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<PagedApi<ReportApi>>() ?? new PagedApi<ReportApi>([], 0, page, 20);
    }

    public async Task<ApiResult> HandleReportAsync(int reportId, string status, bool hideComment, string? token)
    {
        using var req = Build(HttpMethod.Put, $"api/admin/reports/{reportId}", token, new { status, hideComment });
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, status == "Resolved" ? "Đã xử lý báo cáo." : "Đã bác bỏ báo cáo.");
    }

    // ---------- PB31 — Theo dõi truyện + thông báo ----------
    public async Task<FollowStatusApi?> FollowStatusAsync(int storyId, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/stories/{storyId}/follow", token);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<FollowStatusApi>();
    }

    // PB30 — danh sách truyện tôi đang theo dõi (api/me/follows, cần đăng nhập)
    public async Task<List<FollowedStoryApi>> MyFollowsAsync(string? token)
    {
        using var req = Build(HttpMethod.Get, "api/me/follows", token);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return [];
        return await res.Content.ReadFromJsonAsync<List<FollowedStoryApi>>() ?? [];
    }

    // follow = true → theo dõi, false → bỏ theo dõi
    public async Task<ApiResult> SetFollowAsync(int storyId, bool follow, string? token)
    {
        using var req = Build(follow ? HttpMethod.Post : HttpMethod.Delete, $"api/stories/{storyId}/follow", token);
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, follow
            ? "Đã theo dõi truyện. Có chương mới bạn sẽ nhận được thông báo 🔔"
            : "Đã bỏ theo dõi truyện.");
    }

    public async Task<NotificationListApi> NotificationsAsync(int page, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/notifications?page={page}&pageSize=20", token);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<NotificationListApi>() ?? new NotificationListApi([], 0, 0, page, 20);
    }

    // Số thông báo chưa đọc (cho chuông 🔔). Lỗi gì cũng trả 0 để không làm hỏng trang.
    public async Task<int> UnreadCountAsync(string? token)
    {
        try
        {
            using var req = Build(HttpMethod.Get, "api/notifications/unread-count", token);
            using var res = await http.SendAsync(req);
            return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<int>() : 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 0;
        }
    }

    public async Task MarkNotificationReadAsync(int notificationId, string? token)
    {
        using var req = Build(HttpMethod.Put, $"api/notifications/{notificationId}/read", token);
        using var res = await http.SendAsync(req);
    }

    public async Task MarkAllNotificationsReadAsync(string? token)
    {
        using var req = Build(HttpMethod.Put, "api/notifications/read-all", token);
        using var res = await http.SendAsync(req);
    }

    // ---------- Đơn hàng (Backend 3 — PB21 mua truyện) ----------
    // Báo giá mua truyện. Gửi token để biết đã mua chưa / có đơn đang chờ không. Lỗi → null.
    public async Task<OrderQuoteApi?> QuoteStoryAsync(int storyId, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/orders/quote/story/{storyId}", token);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<OrderQuoteApi>();
    }

    // Tạo đơn mua truyện (bấm "Thanh toán"). Đang có đơn chờ thanh toán → API trả lại đơn đó. Thành công → Id = mã đơn.
    public async Task<ApiResult> CreateStoryOrderAsync(int storyId, string? token)
    {
        using var req = Build(HttpMethod.Post, "api/orders", token, new { orderType = "Story", storyId });
        using var res = await http.SendAsync(req);
        var result = await ToResultAsync(res, "Đã tạo đơn hàng.");
        if (!result.Ok) return result;
        var order = await res.Content.ReadFromJsonAsync<OrderApi>();
        return result with { Id = order?.OrderId };
    }

    // Chi tiết đơn (chỉ chủ đơn / Admin). Không có quyền hoặc không tồn tại → null.
    public async Task<OrderApi?> GetOrderAsync(int orderId, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/orders/{orderId}", token);
        using var res = await http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<OrderApi>();
    }


    public async Task<ApiResult> CreatePlanOrderAsync(int planId, int planVersionId, string? token)
    {
        using var req = Build(HttpMethod.Post, "api/orders", token,
            new { orderType = "Plan", planId, planVersionId });
        using var res = await http.SendAsync(req);
        var result = await ToResultAsync(res, "Đã tạo đơn mua gói, đang chờ thanh toán.");
        if (!result.Ok) return result;
        var order = await res.Content.ReadFromJsonAsync<OrderApi>();
        return result with { Id = order?.OrderId };
    }

    public async Task<PagedApi<OrderApi>> MyOrdersAsync(string? token, int page = 1)
    {
        using var req = Build(HttpMethod.Get, $"api/me/orders?page={Math.Max(1, page)}&pageSize=20", token);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<PagedApi<OrderApi>>() ?? new([], 0, page, 20);
    }

    public async Task<PagedApi<OrderApi>> AdminOrdersAsync(string? token, string? status, int page = 1)
    {
        using var req = Build(HttpMethod.Get, $"api/admin/orders?page={Math.Max(1, page)}&pageSize=20&status={Uri.EscapeDataString(status ?? "")}", token);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<PagedApi<OrderApi>>() ?? new([], 0, page, 20);
    }

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
        CreatedAt = c.PublishedAt ?? c.CreatedAt,
        // Giờ hẹn ra mắt (UTC) — chỉ có khi chưa tới giờ, để bảng Admin hiện huy hiệu "Hẹn giờ"
        PublishAt = c.IsUpcoming && c.ScheduledAt.HasValue ? DateTime.SpecifyKind(c.ScheduledAt.Value, DateTimeKind.Utc) : null
    };

    // Đổi giờ UTC từ API sang giờ máy (giờ Việt Nam) để hiển thị
    public static DateTime? ToLocal(DateTime? utc)
        => utc.HasValue ? DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime() : null;

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
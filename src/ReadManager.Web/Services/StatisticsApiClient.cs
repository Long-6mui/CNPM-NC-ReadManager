using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReadManager.Web.Services;

// ===== Kết quả 1 lần gọi API thống kê: có dữ liệu / lỗi / API chưa có =====
public record StatNotice(string Kind, string Message, string? RetryUrl);   // Kind: error | unavailable | empty

public record StatResult<T>(T? Data, string? Error, bool Unavailable)
{
    public bool Ok => Error is null && !Unavailable && Data is not null;
    public static StatResult<T> Success(T data) => new(data, null, false);
    public static StatResult<T> Fail(string message) => new(default, message, false);
    public static StatResult<T> Missing(string message) => new(default, message, true);
    public StatNotice Notice(string? retryUrl) => new(Unavailable ? "unavailable" : "error", Error ?? "Không có dữ liệu.", retryUrl);
}

// ===== PB40 — DTO khớp api/admin/statistics/content (đã có trên main) =====
public record ContentStoryStatApi(int StoryId, string Title, string Visibility, int Reads, int UniqueReaders,
    int NewFollows, int TotalFollowers, int Purchases, int Comments, int InterestScore);
public record ContentGenreStatApi(int GenreId, string Name, int StoryCount, int Reads, int NewFollows, int Purchases, int Comments, int InterestScore);
public record ContentOverviewApi(DateOnly From, DateOnly To, int Reads, int UniqueReaders, int NewFollows, int Purchases, int Comments,
    List<ContentStoryStatApi> TopStories, List<ContentGenreStatApi> TopGenres);
public record ContentDailyApi(DateOnly Date, int Reads, int NewFollows, int Purchases, int Comments);
public record ContentChapterStatApi(int ChapterId, int ChapterNumber, string Title, string AccessLevel, int Reads, int UniqueReaders, int Comments);

// ===== PB37 — DTO khớp api/admin/statistics/activity (nhánh feature/s4-activity--statistics) =====
public class TransactionStatusApi { public string Status { get; set; } = ""; public int OrderCount { get; set; } public decimal TotalAmount { get; set; } public double Percentage { get; set; } }
public class PlanActivityApi
{
    public int PlanId { get; set; } public string PlanName { get; set; } = ""; public bool IsAvailable { get; set; }
    public int NewSubscriptions { get; set; } public int Renewals { get; set; } public int ActiveSubscribers { get; set; }
    public int ExpiringSoonCount { get; set; } public int ExpiredCount { get; set; } public double RenewalRate { get; set; }
}
public class TopReaderApi
{
    public int Rank { get; set; } public string DisplayName { get; set; } = ""; public string MemberTier { get; set; } = "";
    public int ChaptersRead { get; set; } public int CommentsPosted { get; set; } public int PaidOrdersCount { get; set; }
    public decimal TotalSpent { get; set; } public int ActivityScore { get; set; }
}
public class ActivityOverviewApi
{
    public DateOnly From { get; set; } public DateOnly To { get; set; }
    public int TotalMembers { get; set; } public int ActiveAccounts { get; set; } public int LockedAccounts { get; set; }
    public int NewMembers { get; set; } public int ActiveReaders { get; set; } public int ActiveSubscribers { get; set; }
    public int ExpiringSoonSubscriptions { get; set; } public int ChurnedSubscriptions { get; set; }
    public int NewSubscriptions { get; set; } public int Renewals { get; set; } public int StoryPurchases { get; set; }
    public double NewMembersGrowthPercent { get; set; } public double ActiveReadersGrowthPercent { get; set; }
    public double SubscriptionsGrowthPercent { get; set; } public double StoryPurchasesGrowthPercent { get; set; }
    public double RenewalRate { get; set; } public double ConversionRate { get; set; }
    public int? PeakReadingHour { get; set; }
    public int TotalOrders { get; set; } public int PaidOrders { get; set; } public int PendingOrders { get; set; }
    public int FailedOrders { get; set; } public int CancelledOrders { get; set; } public int ExpiredOrders { get; set; }
    public double SuccessRate { get; set; }
    public List<TransactionStatusApi> TransactionsByStatus { get; set; } = [];
    public List<PlanActivityApi> Plans { get; set; } = [];
    public List<TopReaderApi> TopReaders { get; set; } = [];
}
public class ActivityDailyApi
{
    public DateOnly Date { get; set; } public int NewMembers { get; set; } public int ActiveReaders { get; set; }
    public int NewSubscriptions { get; set; } public int Renewals { get; set; } public int StoryPurchases { get; set; }
    public int PaidOrders { get; set; } public int PendingOrders { get; set; } public int FailedOrders { get; set; }
}

// Gọi các API thống kê (chỉ Admin). Mọi hàm đều trả StatResult để View hiển thị đúng trạng thái: có dữ liệu / rỗng / lỗi / API chưa có.
public class StatisticsApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string ContentBase = "api/admin/statistics/content";
    private const string ActivityBase = "api/admin/statistics/activity";

    private static string Range(DateOnly from, DateOnly to) => $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

    // ----- PB40: nội dung -----
    public Task<StatResult<ContentOverviewApi>> ContentOverviewAsync(DateOnly from, DateOnly to, string? token)
        => GetAsync<ContentOverviewApi>($"{ContentBase}/overview?{Range(from, to)}", token, "thống kê nội dung (PB40)");
    public Task<StatResult<List<ContentDailyApi>>> ContentDailyAsync(DateOnly from, DateOnly to, string? token)
        => GetAsync<List<ContentDailyApi>>($"{ContentBase}/daily?{Range(from, to)}", token, "thống kê nội dung theo ngày");
    public Task<StatResult<List<ContentStoryStatApi>>> ContentStoriesAsync(DateOnly from, DateOnly to, string sort, int top, string? token)
        => GetAsync<List<ContentStoryStatApi>>($"{ContentBase}/stories?{Range(from, to)}&sort={Uri.EscapeDataString(sort)}&top={top}", token, "xếp hạng truyện");
    public Task<StatResult<List<ContentGenreStatApi>>> ContentGenresAsync(DateOnly from, DateOnly to, string? token)
        => GetAsync<List<ContentGenreStatApi>>($"{ContentBase}/genres?{Range(from, to)}", token, "thống kê thể loại");
    public Task<StatResult<List<ContentChapterStatApi>>> ContentChaptersAsync(int storyId, DateOnly from, DateOnly to, string? token)
        => GetAsync<List<ContentChapterStatApi>>($"{ContentBase}/stories/{storyId}/chapters?{Range(from, to)}", token, "thống kê chương");

    // ----- PB37: hoạt động -----
    public Task<StatResult<ActivityOverviewApi>> ActivityOverviewAsync(DateOnly from, DateOnly to, string? token)
        => GetAsync<ActivityOverviewApi>($"{ActivityBase}/overview?{Range(from, to)}", token, "thống kê hoạt động (PB37)");
    public Task<StatResult<List<ActivityDailyApi>>> ActivityDailyAsync(DateOnly from, DateOnly to, string? token)
        => GetAsync<List<ActivityDailyApi>>($"{ActivityBase}/daily?{Range(from, to)}", token, "thống kê hoạt động theo ngày");

    // ----- PB38 + PB39: doanh thu — tính từ đơn hàng ĐÃ THANH TOÁN (api/admin/orders?status=Paid) -----
    // Khi Backend 4 có RevenueStatisticsController thì thay hàm này bằng lời gọi API doanh thu.
    public async Task<StatResult<RevenueReport>> RevenueAsync(DateOnly from, DateOnly to, string? token)
    {
        const int pageSize = 100, maxPages = 30;     // tối đa 3.000 đơn
        var all = new List<OrderApi>();
        var truncated = false;
        for (var page = 1; page <= maxPages; page++)
        {
            var r = await GetAsync<PagedApi<OrderApi>>($"api/admin/orders?status=Paid&page={page}&pageSize={pageSize}", token, "đơn hàng (doanh thu)");
            if (!r.Ok) return new StatResult<RevenueReport>(null, r.Error, r.Unavailable);
            var items = r.Data!.Items;
            all.AddRange(items);
            if (items.Count < pageSize || all.Count >= r.Data.TotalCount) break;
            // Danh sách mới nhất trước: cả trang đều tạo trước kỳ xem 2 ngày → các trang sau chắc chắn ngoài kỳ
            if (items.All(o => o.CreatedAt < from.ToDateTime(TimeOnly.MinValue).AddDays(-2))) break;
            if (page == maxPages) truncated = true;
        }
        return StatResult<RevenueReport>.Success(RevenueReport.Build(all, from, to, truncated));
    }

    private async Task<StatResult<T>> GetAsync<T>(string url, string? token, string what)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await http.SendAsync(req);
            if (res.StatusCode == HttpStatusCode.NotFound)
                return StatResult<T>.Missing($"API {what} chưa có trên máy chủ API đang chạy. Cần merge nhánh Backend tương ứng rồi chạy lại API.");
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return StatResult<T>.Fail("Phiên đăng nhập Admin đã hết hạn hoặc không đủ quyền. Hãy đăng nhập lại.");
            if (res.StatusCode == HttpStatusCode.BadRequest)
                return StatResult<T>.Fail(await ReadMessageAsync(res) ?? "Khoảng thời gian không hợp lệ.");
            if (!res.IsSuccessStatusCode)
                return StatResult<T>.Fail($"API trả về lỗi {(int)res.StatusCode}. Vui lòng thử lại.");
            var data = await res.Content.ReadFromJsonAsync<T>(Json);
            return data is null ? StatResult<T>.Fail("API không trả dữ liệu.") : StatResult<T>.Success(data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatResult<T>.Fail("Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại."); }
        catch (JsonException)
        { return StatResult<T>.Fail("Dữ liệu thống kê trả về không đọc được (sai định dạng)."); }
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage res)
    {
        try { using var d = JsonDocument.Parse(await res.Content.ReadAsStringAsync()); return d.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null; }
        catch { return null; }
    }
}

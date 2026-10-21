using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReadManager.Web.Services;

// Các record khớp với DTO của API: ReadingPlanDtos.cs (api/reading-plans)
public record PlanVersionApi(int PlanVersionId, int VersionNumber, decimal Price, int DurationMonths,
    string ScopeType, DateTime CreatedAt, DateTime? PublishedAt, List<int> StoryIds);
public record ReadingPlanApi(int PlanId, string Name, bool IsAvailable, DateTime CreatedAt, PlanVersionApi? CurrentVersion);
public record PlanPageApi(List<ReadingPlanApi> Items, int TotalCount, int Page, int PageSize);

// PB17 + PB18 — gọi API gói đọc (công khai + quản trị)
public class PlansApiClient(HttpClient http)
{
    // ----- Công khai -----
    public async Task<List<ReadingPlanApi>> ListAsync()
    {
        var page = await http.GetFromJsonAsync<PlanPageApi>("api/reading-plans?page=1&pageSize=100");
        return page?.Items ?? [];
    }

    public async Task<ReadingPlanApi?> GetAsync(int id)
    {
        using var res = await http.GetAsync($"api/reading-plans/{id}");
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<ReadingPlanApi>();
    }

    // ----- Quản trị (cần token Admin) -----
    public async Task<List<ReadingPlanApi>> AdminListAsync(string? token)
    {
        using var req = Build(HttpMethod.Get, "api/reading-plans/admin?page=1&pageSize=100", token);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PlanPageApi>())?.Items ?? [];
    }

    public async Task<List<PlanVersionApi>> VersionsAsync(int planId, string? token)
    {
        using var req = Build(HttpMethod.Get, $"api/reading-plans/{planId}/versions", token);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return [];
        return await res.Content.ReadFromJsonAsync<List<PlanVersionApi>>() ?? [];
    }

    // PB17 — tạo gói mới (kèm phiên bản 1)
    public async Task<ApiResult> CreateAsync(string name, decimal price, int months, string scope, List<int> storyIds, string? token)
    {
        using var req = Build(HttpMethod.Post, "api/reading-plans", token,
            new { name, price, durationMonths = months, scopeType = scope, storyIds });
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, "Đã tạo gói đọc mới.");
    }

    // PB18 — đổi giá / thời hạn / phạm vi = phát hành phiên bản mới (phiên bản cũ giữ nguyên)
    public async Task<ApiResult> AddVersionAsync(int planId, decimal price, int months, string scope, List<int> storyIds, string? token)
    {
        using var req = Build(HttpMethod.Post, $"api/reading-plans/{planId}/versions", token,
            new { price, durationMonths = months, scopeType = scope, storyIds });
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, "Đã phát hành phiên bản mới của gói.");
    }

    public async Task<ApiResult> RenameAsync(int planId, string name, string? token)
    {
        using var req = Build(HttpMethod.Patch, $"api/reading-plans/{planId}/name", token, new { name });
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, "Đã đổi tên gói.");
    }

    // PB18 — ngừng bán / bán lại (không xóa gói, không thu hồi quyền đã cấp)
    public async Task<ApiResult> SetAvailabilityAsync(int planId, bool isAvailable, string? token)
    {
        using var req = Build(HttpMethod.Patch, $"api/reading-plans/{planId}/availability", token, new { isAvailable });
        using var res = await http.SendAsync(req);
        return await ToResultAsync(res, isAvailable ? "Gói đã được bán lại." : "Đã ngừng bán gói. Quyền đã cấp vẫn còn hiệu lực.");
    }

    private static HttpRequestMessage Build(HttpMethod method, string url, string? token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    // API trả {message} (BadRequest tự viết) hoặc ProblemDetails {errors:{Field:[..]}} (validation)
    private static async Task<ApiResult> ToResultAsync(HttpResponseMessage res, string okMessage)
    {
        if (res.IsSuccessStatusCode) return new ApiResult(true, okMessage);
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return new ApiResult(false, m.GetString()!);
            if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
            {
                var first = errs.EnumerateObject().SelectMany(p => p.Value.EnumerateArray()).Select(e => e.GetString()).FirstOrDefault(s => !string.IsNullOrEmpty(s));
                if (first is not null) return new ApiResult(false, first);
            }
        }
        catch { }
        return new ApiResult(false, res.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Phiên đăng nhập hết hạn. Hãy đăng nhập lại.",
            HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            HttpStatusCode.NotFound => "Không tìm thấy gói đọc.",
            HttpStatusCode.BadRequest => "Dữ liệu không hợp lệ.",
            _ => $"API trả về lỗi {(int)res.StatusCode}."
        });
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReadManager.Web.Services;

public record PaymentStartApi(int PaymentId, string MerchantRequestId, string Provider, int OrderId, decimal Amount, string Currency);
public record PaymentOutcomeApi(string Result, string? Reason, string Message, int OrderId, string? OrderStatus, bool EntitlementGranted);
public record PaymentRecoveryApi(int PaymentId, int OrderId, int UserId, string OrderType, string ItemName, string OrderStatus, decimal? Amount, string? ProviderTransactionId, DateTime? SucceededAt, string? LastError);
public record RegrantApi(int OrderId, bool Granted, bool AlreadyGranted, string Message);
public record SubscriptionApi(int SubscriptionId, int OrderId, int? PlanId, int? PlanVersionId, string PlanName, string ScopeType, DateTime StartsAt, DateTime EndsAt, DateTime? RevokedAt, bool IsActive, int DaysRemaining, bool IsExpiringSoon, string StatusBadge, bool CanRenew);
public record SubscriptionSummaryApi(SubscriptionApi? ActiveSubscription, bool HasExpiringAlert, string? AlertMessage, List<SubscriptionApi> History);
public record PurchasedStoryApi(int StoryId, string Title, string Slug, string AuthorName, string? CoverUrl, DateTime GrantedAt, DateTime? ExpiresAt);
public record UserLibraryApi(List<PurchasedStoryApi> PurchasedStories, SubscriptionSummaryApi Subscriptions);
public record PaymentApiReply<T>(bool Ok, string Message, T? Data);

public class PaymentsApiClient(HttpClient http)
{
    public Task<PaymentApiReply<PaymentStartApi>> StartAsync(int orderId, string? token)
        => Send<PaymentStartApi>(HttpMethod.Post, "api/payments/start", token, new { orderId });
    public Task<PaymentApiReply<PaymentOutcomeApi>> SimulateAsync(int orderId, string status, string? token)
        => Send<PaymentOutcomeApi>(HttpMethod.Post, "api/payments/dev/simulate", token, new { orderId, status });
    public Task<PaymentApiReply<UserLibraryApi>> LibraryAsync(string? token)
        => Send<UserLibraryApi>(HttpMethod.Get, "api/me/library", token);
    public Task<PaymentApiReply<PagedApi<PaymentRecoveryApi>>> RecoveryAsync(int page, string? token)
        => Send<PagedApi<PaymentRecoveryApi>>(HttpMethod.Get, $"api/admin/payments/needs-recovery?page={Math.Max(1,page)}&pageSize=20", token);
    public Task<PaymentApiReply<RegrantApi>> RegrantAsync(int orderId, string? token)
        => Send<RegrantApi>(HttpMethod.Post, $"api/admin/payments/orders/{orderId}/regrant", token);

    private async Task<PaymentApiReply<T>> Send<T>(HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>();
            return new(data is not null, data is null ? "API trả dữ liệu trống." : "", data);
        }
        string? message = null;
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) message = m.GetString();
        }
        catch (JsonException) { }
        return new(false, message ?? ((int)response.StatusCode switch
        {
            401 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            403 => "Bạn không có quyền thực hiện thao tác này.",
            404 => "Không tìm thấy dữ liệu hoặc chức năng không được bật trên API.",
            _ => $"API trả lỗi {(int)response.StatusCode}. Vui lòng thử lại."
        }), default);
    }
}

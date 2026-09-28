using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

// Wrapper dùng chung cho mọi service gọi API. Đăng ký trong DI bằng
// builder.Services.AddHttpClient<ApiClient>(c => c.BaseAddress = new Uri(ApiSettings.BaseUrl));
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStore _tokenStore;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http, ITokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    private async Task AttachAuthHeaderAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string uri)
    {
        await AttachAuthHeaderAsync();
        using var response = await _http.GetAsync(uri);
        return await HandleResponseAsync<T>(response);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string uri, TRequest body)
    {
        await AttachAuthHeaderAsync();
        using var response = await _http.PostAsJsonAsync(uri, body, JsonOptions);
        return await HandleResponseAsync<TResponse>(response);
    }

    public async Task PostAsync<TRequest>(string uri, TRequest body)
    {
        await AttachAuthHeaderAsync();
        using var response = await _http.PostAsJsonAsync(uri, body, JsonOptions);
        await HandleResponseAsync(response);
    }

    private static async Task<T?> HandleResponseAsync<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = JsonSerializer.Deserialize<ApiError>(raw, JsonOptions); }
            catch { /* body không phải JSON hợp lệ, bỏ qua */ }

            throw new ApiException(
                (int)response.StatusCode,
                error?.Message ?? $"Lỗi máy chủ ({(int)response.StatusCode})",
                error);
        }

        if (string.IsNullOrWhiteSpace(raw)) return default;
        return JsonSerializer.Deserialize<T>(raw, JsonOptions);
    }

    private static async Task HandleResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync();
            ApiError? error = null;
            try { error = JsonSerializer.Deserialize<ApiError>(raw, JsonOptions); }
            catch { /* bỏ qua */ }

            throw new ApiException(
                (int)response.StatusCode,
                error?.Message ?? $"Lỗi máy chủ ({(int)response.StatusCode})",
                error);
        }
    }
}

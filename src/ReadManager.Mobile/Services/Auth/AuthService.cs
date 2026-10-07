using ReadManager.Mobile.Models.Auth;

namespace ReadManager.Mobile.Services.Auth;

public class AuthService : IAuthService
{
    private readonly ApiClient _api;
    private readonly ITokenStore _tokenStore;

    // Register returns user information; only login creates a session.
    private const string RegisterEndpoint = "auth/register";
    private const string LoginEndpoint = "auth/login";

    public UserProfile? CurrentUser { get; private set; }

    public AuthService(ApiClient api, ITokenStore tokenStore)
    {
        _api = api;
        _tokenStore = tokenStore;
    }

    public async Task<UserProfile> RegisterAsync(RegisterRequest request)
    {
        var result = await _api.PostAsync<RegisterRequest, RegistrationResponse>(RegisterEndpoint, request)
            ?? throw new ApiException(0, "Không nhận được phản hồi từ máy chủ.");

        return result.User;
    }

    public async Task<UserProfile> LoginAsync(LoginRequest request)
    {
        var result = await _api.PostAsync<LoginRequest, AuthResponse>(LoginEndpoint, request)
            ?? throw new ApiException(0, "Không nhận được phản hồi từ máy chủ.");

        await PersistSessionAsync(result);
        return result.User;
    }

    public async Task LogoutAsync()
    {
        try { await _api.PostAsync("auth/logout", new { }); }
        catch (ApiException ex) when (ex.StatusCode is 401 or 403) { }
        finally { CurrentUser = null; await _tokenStore.ClearAsync(); }
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        if (string.IsNullOrEmpty(token)) { CurrentUser = null; return false; }
        try
        {
            CurrentUser = await _api.GetAsync<UserProfile>("auth/me");
            return CurrentUser is not null;
        }
        catch (ApiException ex) when (ex.StatusCode is 401 or 403)
        { CurrentUser = null; await _tokenStore.ClearAsync(); return false; }
    }

    private async Task PersistSessionAsync(AuthResponse result)
    {
        if (string.IsNullOrWhiteSpace(result.Token)) throw new ApiException(0, "Phản hồi đăng nhập thiếu token.");
        await _tokenStore.SaveTokenAsync(result.Token);
        CurrentUser = result.User;
    }
}

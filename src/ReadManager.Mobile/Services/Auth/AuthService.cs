using ReadManager.Mobile.Models.Auth;

namespace ReadManager.Mobile.Services.Auth;

public class AuthService : IAuthService
{
    private readonly ApiClient _api;
    private readonly ITokenStore _tokenStore;

    // TODO: xác nhận lại route thật với Backend 2 (AuthController) — đang giả định
    // POST api/auth/register và POST api/auth/login trả về AuthResponse { token, user }.
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
        var result = await _api.PostAsync<RegisterRequest, AuthResponse>(RegisterEndpoint, request)
            ?? throw new ApiException(0, "Không nhận được phản hồi từ máy chủ.");

        await PersistSessionAsync(result);
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
        CurrentUser = null;
        await _tokenStore.ClearAsync();
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    private async Task PersistSessionAsync(AuthResponse result)
    {
        await _tokenStore.SaveTokenAsync(result.Token);
        CurrentUser = result.User;
    }
}

using ReadManager.Mobile.Models.Auth;

namespace ReadManager.Mobile.Services.Auth;

public interface IAuthService
{
    Task<UserProfile> RegisterAsync(RegisterRequest request);
    Task<UserProfile> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    UserProfile? CurrentUser { get; }
}

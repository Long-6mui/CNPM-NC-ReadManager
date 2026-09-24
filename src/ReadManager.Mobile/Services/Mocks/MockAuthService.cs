using ReadManager.Mobile.Models.Auth;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.Services.Mocks;

// Dùng tạm khi Backend chưa xong API auth thật — cho phép đăng nhập/đăng ký với bất kỳ
// thông tin gì để xem giao diện các màn còn lại. KHÔNG gọi mạng.
public class MockAuthService : IAuthService
{
    public UserProfile? CurrentUser { get; private set; }

    public Task<UserProfile> RegisterAsync(RegisterRequest request) => LoginInternalAsync(request.Username, request.DisplayName);

    public Task<UserProfile> LoginAsync(LoginRequest request) => LoginInternalAsync(request.UsernameOrEmail, request.UsernameOrEmail);

    private Task<UserProfile> LoginInternalAsync(string username, string displayName)
    {
        CurrentUser = new UserProfile
        {
            UserId = 1,
            Username = string.IsNullOrWhiteSpace(username) ? "demo" : username,
            Email = "demo@example.com",
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Người dùng demo" : displayName,
            Role = "Member"
        };
        return Task.FromResult(CurrentUser);
    }

    public Task LogoutAsync()
    {
        CurrentUser = null;
        return Task.CompletedTask;
    }

    public Task<bool> IsLoggedInAsync() => Task.FromResult(CurrentUser is not null);
}

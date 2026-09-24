namespace ReadManager.Mobile.Models.Auth;

public class LoginRequest
{
    // Cho phép đăng nhập bằng username hoặc email — tuỳ Backend 2 quyết định,
    // để "UsernameOrEmail" cho linh hoạt, không cần đổi model nếu BE hỗ trợ cả hai.
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

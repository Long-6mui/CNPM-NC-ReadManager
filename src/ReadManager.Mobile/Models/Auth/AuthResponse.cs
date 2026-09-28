namespace ReadManager.Mobile.Models.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public UserProfile User { get; set; } = new();
}

public class UserProfile
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Member";
}

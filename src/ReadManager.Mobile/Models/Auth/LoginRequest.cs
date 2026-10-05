using System.Text.Json.Serialization;

namespace ReadManager.Mobile.Models.Auth;

public class LoginRequest
{
    // API (AuthController.LoginRequest) nhận trường "email"
    [JsonPropertyName("email")]
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

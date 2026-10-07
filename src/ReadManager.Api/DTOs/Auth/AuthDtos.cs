using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Auth;

// DTO gửi lên khi Đăng ký
public class RegisterRequestDto
{
    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống.")]
    [StringLength(254)]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [StringLength(100)]
    [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên.")]
    public string Password { get; set; } = string.Empty;
}

// DTO gửi lên khi Đăng nhập
public class LoginRequestDto
{
    [Required(ErrorMessage = "Email không được để trống.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

// DTO chứa thông tin User trả về
public class UserDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
}

// DTO kết quả Đăng nhập khớp 100% với LoginResult của Web
public class LoginResultDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}
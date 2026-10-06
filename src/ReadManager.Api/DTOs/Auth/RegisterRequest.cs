using System.ComponentModel.DataAnnotations;
namespace ReadManager.Api.DTOs.Auth;

public record RegisterRequest(
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    string Email,

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 100 ký tự.")]
    string Password,

    [Required(ErrorMessage = "Tên hiển thị không được để trống.")]
    [StringLength(100, ErrorMessage = "Tên hiển thị không được vượt quá 100 ký tự.")]
    string DisplayName
);
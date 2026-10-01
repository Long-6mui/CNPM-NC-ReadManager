using System.ComponentModel.DataAnnotations;
namespace ReadManager.Api.DTOs.Auth;

public record LoginRequest(
    [Required, EmailAddress]
    string Email,

    [Required, StringLength(100)]
    string Password
);
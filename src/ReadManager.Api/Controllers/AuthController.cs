using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReadManager.Api.DTOs.Auth;
using ReadManager.Api.Services;
using System.Security.Claims;

namespace ReadManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Đăng ký tài khoản (POST /api/auth/register)
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, errorMessage, user) = await _authService.RegisterAsync(request);

        if (!success)
        {
            // Trả về 409 Conflict khi trùng email
            return Conflict(new { message = errorMessage });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Đăng ký tài khoản thành công.",
            user
        });
    }

    /// <summary>
    /// Đăng nhập hệ thống (POST /api/auth/login)
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")] // tối đa 10 lần/phút/IP (policy khai báo trong Program.cs)
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, errorMessage, loginResult, isLocked) = await _authService.LoginAsync(request);

        if (!success)
        {
            // 403 khi tài khoản bị khóa (Web hiển thị "Tài khoản đã bị khóa"), 401 khi sai thông tin
            return isLocked
                ? StatusCode(StatusCodes.Status403Forbidden, new { message = errorMessage })
                : Unauthorized(new { message = errorMessage });
        }

        return Ok(loginResult);
    }

    /// <summary>
    /// Đăng xuất (POST /api/auth/logout)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await _authService.LogoutAsync(userId); // thu hồi token
        }

        return Ok(new { message = "Đăng xuất thành công." });
    }

    /// <summary>
    /// Lấy thông tin tài khoản hiện tại (GET /api/auth/me)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        return Ok(user);
    }
}
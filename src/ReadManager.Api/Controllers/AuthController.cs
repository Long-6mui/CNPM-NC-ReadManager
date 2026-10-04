using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Authentication;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;
using ReadManager.Api.DTOs.Auth; // Gọi đến thư mục chứa 2 file DTO vừa tạo

namespace ReadManager.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, IDataProtectionProvider protection) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email này đã được sử dụng." });

        var user = new User
        {
            Username = $"user_{Guid.NewGuid():N}",
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = "Member",
            AccountStatus = "Active",
            SecurityVersion = 1
        };

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);

        try
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "Email này đã được sử dụng." });
            throw;
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Đăng ký thành công.",
            user = new { user.UserId, user.Email, user.DisplayName, user.Role }
        });
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null || user.AccountStatus != "Active")
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng, hoặc tài khoản không hoạt động." });

        PasswordVerificationResult verified;
        try { verified = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password); }
        catch (FormatException) { verified = PasswordVerificationResult.Failed; }

        if (verified == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng, hoặc tài khoản không hoạt động." });

        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
            await db.SaveChangesAsync();
        }

        var expires = DateTimeOffset.UtcNow.AddHours(8);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("security_version", user.SecurityVersion.ToString())
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiSessionHandler.SchemeName));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = expires }, ApiSessionHandler.SchemeName);

        return Ok(new { accessToken = ApiSessionHandler.Format(protection).Protect(ticket), expiresAt = expires, user = new { user.UserId, user.Email, user.DisplayName, user.Role } });
    }

    [Authorize, HttpGet("me")]
    public IActionResult Me() => Ok(new { userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), email = User.FindFirstValue(ClaimTypes.Email), displayName = User.Identity!.Name, role = User.FindFirstValue(ClaimTypes.Role) });

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await db.Users.Where(x => x.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SecurityVersion, x => x.SecurityVersion + 1));
        return NoContent();
    }
}
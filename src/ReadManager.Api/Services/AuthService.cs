using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Authentication;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Auth;
using ReadManager.Api.Entities;
using System.Security.Claims;

namespace ReadManager.Api.Services;

public interface IAuthService
{
    Task<(bool Success, string? ErrorMessage, UserDto? User)> RegisterAsync(RegisterRequestDto request);
    Task<(bool Success, string? ErrorMessage, LoginResultDto? Data, bool IsLocked)> LoginAsync(LoginRequestDto request);
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task LogoutAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly TicketDataFormat _ticketFormat;

    public AuthService(AppDbContext db, IDataProtectionProvider dataProtection)
    {
        _db = db;
        _ticketFormat = ApiSessionHandler.Format(dataProtection);
    }

    public async Task<(bool Success, string? ErrorMessage, UserDto? User)> RegisterAsync(RegisterRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Kiểm tra trùng Email
        var emailExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (emailExists)
        {
            return (false, "Email này đã được sử dụng. Vui lòng chọn email khác.", null);
        }

        // 2. Tạo User mới (Mặc định Role là User)
        var baseUsername = normalizedEmail.Split('@')[0];
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];

        var newUser = new User
        {
            Username = $"{baseUsername}_{uniqueSuffix}",
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? baseUsername : request.DisplayName.Trim(),
            Role = "Member", // Mặc định là quyền Member (khớp Entities/User.cs)
            AccountStatus = "Active",
            SecurityVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 3. Băm mật khẩu an toàn
        newUser.PasswordHash = new PasswordHasher<User>().HashPassword(newUser, request.Password);

        _db.Users.Add(newUser);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hai người đăng ký cùng email cùng lúc: chỉ số UNIQUE trong DB sẽ chặn người đến sau.
            return (false, "Email này đã được sử dụng. Vui lòng chọn email khác.", null);
        }

        var userDto = new UserDto
        {
            UserId = newUser.UserId,
            Email = newUser.Email,
            DisplayName = newUser.DisplayName,
            Role = newUser.Role
        };

        return (true, null, userDto);
    }

    public async Task<(bool Success, string? ErrorMessage, LoginResultDto? Data, bool IsLocked)> LoginAsync(LoginRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Tìm tài khoản theo Email hoặc Username
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail || u.Username.ToLower() == normalizedEmail);

        if (user == null)
        {
            return (false, "Email hoặc mật khẩu không chính xác.", null, false);
        }

        // Kiểm tra mật khẩu băm
        var verifyResult = new PasswordHasher<User>()
            .VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return (false, "Email hoặc mật khẩu không chính xác.", null, false);
        }

        if (user.AccountStatus != "Active")
        {
            return (false, "Tài khoản của bạn đang bị khóa.", null, true);
        }

        // Tạo token đúng định dạng mà ApiSessionHandler đọc được (AuthenticationTicket + TicketDataFormat)
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8); // khớp doc/ADMIN_LOGIN.md và cookie Web (8 giờ)
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("security_version", user.SecurityVersion.ToString())
        }, ApiSessionHandler.SchemeName);

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { ExpiresUtc = expiresAt },
            ApiSessionHandler.SchemeName);

        var accessToken = _ticketFormat.Protect(ticket);

        var loginResult = new LoginResultDto
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                UserId = user.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role // Admin hoặc Member
            }
        };

        return (true, null, loginResult, false);
    }

    public async Task LogoutAsync(int userId)
    {
        // Tăng SecurityVersion => mọi token cũ của tài khoản này bị ApiSessionHandler từ chối ("Session revoked").
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        user.SecurityVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;

        return new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role
        };
    }
}
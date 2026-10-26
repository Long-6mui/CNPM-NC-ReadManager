using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.DTOs.Users;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// PB35 — ADMIN XEM DANH SÁCH, TÌNH TRẠNG VÀ THÔNG TIN SỬ DỤNG DỊCH VỤ CỦA THÀNH VIÊN
// Chỉ ĐỌC dữ liệu. Việc đổi trạng thái (hạn chế / tạm ngừng) nằm ở UserRestrictionService.
public interface IUserManagementService
{
    // role: null/rỗng = chỉ thành viên (Member); "Admin" = quản trị viên; "all" = tất cả
    // Ném ArgumentException nếu status / role không hợp lệ.
    Task<PagedResultDto<MemberListItemDto>> GetListAsync(
        string? keyword, string? status, string? role, int page, int pageSize);

    // Trả null = không tìm thấy
    Task<MemberDetailDto?> GetDetailAsync(int userId);
}

public class UserManagementService : IUserManagementService
{
    private const int MaxPageSize = 50;
    private const int MaxKeywordLength = 100;
    private const string MemberRole = "Member";
    private const string AdminRole = "Admin";

    private readonly AppDbContext _db;

    public UserManagementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<MemberListItemDto>> GetListAsync(
        string? keyword, string? status, string? role, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        // Tài khoản hệ thống (Disabled — vd: nguồn import ZIP) không phải thành viên thật → luôn ẩn
        var query = _db.Users.AsNoTracking()
            .Where(u => u.AccountStatus != AccountStatuses.Disabled);

        // --- lọc theo vai trò (mặc định: chỉ Member) ---
        if (string.IsNullOrWhiteSpace(role) || role.Trim().Equals(MemberRole, StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.Role == MemberRole);
        else if (role.Trim().Equals(AdminRole, StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.Role == AdminRole);
        else if (!role.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Vai trò không hợp lệ. Chọn: Member, Admin hoặc all.");

        // --- lọc theo trạng thái ---
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = AccountStatuses.Normalize(status)
                ?? throw new ArgumentException("Trạng thái không hợp lệ. Chọn: Active, Restricted hoặc Suspended.");
            query = query.Where(u => u.AccountStatus == normalized);
        }

        // --- tìm theo email / tên hiển thị ---
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            if (kw.Length > MaxKeywordLength) kw = kw[..MaxKeywordLength];
            query = query.Where(u => u.Email.Contains(kw) || u.DisplayName.Contains(kw));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new MemberListItemDto
            {
                UserId = u.UserId,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role,
                AccountStatus = u.AccountStatus,
                CreatedAt = u.CreatedAt,
                ChapterReadCount = _db.ChapterReads.Count(r => r.UserId == u.UserId),
                LastReadAt = _db.ChapterReads
                    .Where(r => r.UserId == u.UserId)
                    .Max(r => (DateTime?)r.ReadAt)
            })
            .ToListAsync();

        return new PagedResultDto<MemberListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<MemberDetailDto?> GetDetailAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId && u.AccountStatus != AccountStatuses.Disabled)
            .Select(u => new MemberDetailDto
            {
                UserId = u.UserId,
                Email = u.Email,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                Role = u.Role,
                AccountStatus = u.AccountStatus,
                StatusReason = u.StatusReason,
                StatusChangedAt = u.StatusChangedAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .FirstOrDefaultAsync();
        if (user is null)
            return null;

        var now = DateTime.UtcNow;

        // Đọc truyện
        user.ChapterReadCount = await _db.ChapterReads.CountAsync(r => r.UserId == userId);
        user.LastReadAt = await _db.ChapterReads
            .Where(r => r.UserId == userId)
            .MaxAsync(r => (DateTime?)r.ReadAt);
        user.ReadingStoryCount = await _db.ReadingProgresses.CountAsync(p => p.UserId == userId);

        // Tương tác
        user.FollowCount = await _db.StoryFollows.CountAsync(f => f.UserId == userId);
        user.CommentCount = await _db.Comments.CountAsync(c => c.UserId == userId);

        // Thanh toán
        user.PaidOrderCount = await _db.Orders.CountAsync(o => o.UserId == userId && o.Status == "Paid");
        user.TotalPaid = await _db.Orders
            .Where(o => o.UserId == userId && o.Status == "Paid")
            .SumAsync(o => (decimal?)o.Amount) ?? 0m;

        // Gói đọc còn hạn (chưa bị thu hồi, đang trong thời hạn)
        user.PlanEndsAt = await _db.UserSubscriptions
            .Where(s => s.Order.UserId == userId
                     && s.RevokedAt == null
                     && s.StartsAt <= now
                     && s.EndsAt > now)
            .MaxAsync(s => (DateTime?)s.EndsAt);
        user.HasActivePlan = user.PlanEndsAt.HasValue;

        return user;
    }
}

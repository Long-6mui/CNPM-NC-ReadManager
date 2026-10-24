using System.Text;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Users;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// PB36 — HẠN CHẾ HOẶC TẠM NGỪNG TÀI KHOẢN VI PHẠM
//
//   Restricted (hạn chế)   : vẫn đăng nhập + đọc, nhưng KHÔNG được bình luận / báo cáo / theo dõi truyện
//   Suspended (tạm ngừng)  : không đăng nhập được, các phiên đang đăng nhập bị thu hồi ngay
//   Active (bình thường)   : dùng để MỞ LẠI tài khoản (khi Admin khóa nhầm / hết hạn xử phạt)
public interface IUserRestrictionService
{
    // Đổi trạng thái 1 tài khoản. Trả null = không tìm thấy tài khoản.
    //   ArgumentException          → dữ liệu sai (trạng thái lạ, thiếu lý do)     → controller trả 400
    //   UnauthorizedAccessException → vi phạm quy tắc (tự khóa mình, khóa Admin)  → controller trả 403
    Task<UserStatusResultDto?> ChangeStatusAsync(int adminId, int targetUserId, ChangeUserStatusDto dto);

    // Tài khoản này có được tương tác (bình luận, báo cáo, theo dõi...) không?
    // Dùng bởi ActiveAccountHandler → chặn ở tầng phân quyền, không cần sửa từng service.
    Task<bool> CanInteractAsync(int userId);
}

public class UserRestrictionService : IUserRestrictionService
{
    private const int MaxReasonLength = 500;
    private const string AdminRole = "Admin";

    private readonly AppDbContext _db;

    public UserRestrictionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserStatusResultDto?> ChangeStatusAsync(int adminId, int targetUserId, ChangeUserStatusDto dto)
    {
        // 1. Trạng thái phải hợp lệ
        var newStatus = AccountStatuses.Normalize(dto.Status)
            ?? throw new ArgumentException("Trạng thái không hợp lệ. Chọn: Active, Restricted hoặc Suspended.");

        // 2. Hạn chế / tạm ngừng thì BẮT BUỘC có lý do (để truy lại, tránh khóa nhầm không rõ vì sao)
        var reason = string.IsNullOrWhiteSpace(dto.Reason)
            ? null
            : dto.Reason.Normalize(NormalizationForm.FormC).Trim();
        if (reason is { Length: > MaxReasonLength })
            throw new ArgumentException($"Lý do tối đa {MaxReasonLength} ký tự.");
        if (newStatus != AccountStatuses.Active && reason is null)
            throw new ArgumentException("Vui lòng nhập lý do hạn chế / tạm ngừng tài khoản.");

        // 3. Tìm tài khoản (tài khoản hệ thống "Disabled" coi như không tồn tại)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == targetUserId);
        if (user is null || user.AccountStatus == AccountStatuses.Disabled)
            return null;

        // 4. Quy tắc an toàn
        if (user.UserId == adminId)
            throw new UnauthorizedAccessException("Bạn không thể thay đổi trạng thái tài khoản của chính mình.");
        if (user.Role == AdminRole)
            throw new UnauthorizedAccessException("Không thể hạn chế hoặc tạm ngừng tài khoản Admin.");

        // 5. Đã đúng trạng thái đó rồi → không làm gì (bấm 2 lần không lỗi, không ghi đè lý do cũ)
        if (user.AccountStatus == newStatus)
            return ToResult(user);

        var now = DateTime.UtcNow;
        user.AccountStatus = newStatus;
        user.StatusReason = newStatus == AccountStatuses.Active ? null : reason;
        user.StatusChangedAt = now;
        user.UpdatedAt = now;

        // Tạm ngừng → tăng SecurityVersion để THU HỒI mọi token cũ (ApiSessionHandler so khớp số này).
        // Nhờ vậy khi mở lại tài khoản, token cũ vẫn không dùng được lại → phải đăng nhập mới.
        if (newStatus == AccountStatuses.Suspended)
            user.SecurityVersion++;

        await _db.SaveChangesAsync();
        return ToResult(user);
    }

    public async Task<bool> CanInteractAsync(int userId)
    {
        var status = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.AccountStatus)
            .FirstOrDefaultAsync();
        return AccountStatuses.CanInteract(status);
    }

    private static UserStatusResultDto ToResult(User user) => new()
    {
        UserId = user.UserId,
        AccountStatus = user.AccountStatus,
        StatusReason = user.StatusReason,
        StatusChangedAt = user.StatusChangedAt
    };
}

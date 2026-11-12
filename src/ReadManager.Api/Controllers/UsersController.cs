using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.DTOs.Users;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 2 — PB35 + PB36: QUẢN LÝ THÀNH VIÊN (CHỈ ADMIN)
//   GET api/admin/users?keyword=&status=&role=&page=1&pageSize=20  → PB35: danh sách + tình trạng
//   GET api/admin/users/7                                          → PB35: chi tiết + thông tin sử dụng dịch vụ
//   PUT api/admin/users/7/status                                   → PB36: hạn chế / tạm ngừng / mở lại
//
// [Authorize(Roles = "Admin")] đặt ở cấp class → MỌI API trong file này đều chặn người không phải Admin
// (khách → 401, Member → 403). Đây là điểm quan trọng nhất để không lộ dữ liệu cá nhân (rủi ro của PB35).
[ApiController]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _managementService;
    private readonly IUserRestrictionService _restrictionService;

    public UsersController(IUserManagementService managementService, IUserRestrictionService restrictionService)
    {
        _managementService = managementService;
        _restrictionService = restrictionService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record ReaderGrant(Guid RequestId, string Email, int Coins, int Days);

    [HttpPost("api/admin/users/reader-grants")]
    public async Task<IActionResult> Grant(ReaderGrant request, [FromServices] IStreakService service)
    {
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email) ||
            request.Coins < 0 || request.Days < 0 || (request.Coins == 0) == (request.Days == 0))
            return BadRequest(new { message = "Chọn một khoản xu hoặc số ngày chuỗi lớn hơn 0." });
        try
        {
            var found = await service.GrantAsync(CurrentUserId, request.RequestId, request.Email.Trim(), request.Coins, request.Days);
            return found ? NoContent() : NotFound(new { message = "Không tìm thấy tài khoản có email này." });
        }
        catch (OverflowException) { return Conflict(new { message = "Giá trị vượt giới hạn lưu trữ." }); }
    }

    [HttpGet("api/admin/users")]
    public async Task<ActionResult<PagedResultDto<MemberListItemDto>>> GetList(
        [FromQuery] string? keyword, [FromQuery] string? status, [FromQuery] string? role,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            return Ok(await _managementService.GetListAsync(keyword, status, role, page, pageSize));
        }
        catch (ArgumentException ex)   // status / role không hợp lệ
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("api/admin/users/{userId:int}")]
    public async Task<ActionResult<MemberDetailDto>> GetDetail(int userId)
    {
        var member = await _managementService.GetDetailAsync(userId);
        return member is null ? NotFound(new { message = "Không tìm thấy thành viên." }) : Ok(member);
    }

    // body: { "status": "Restricted", "reason": "Spam bình luận" }
    //       { "status": "Suspended",  "reason": "Phát tán nội dung vi phạm" }
    //       { "status": "Active" }                      ← mở lại tài khoản
    [HttpPut("api/admin/users/{userId:int}/status")]
    public async Task<ActionResult<UserStatusResultDto>> ChangeStatus(int userId, ChangeUserStatusDto dto)
    {
        try
        {
            var result = await _restrictionService.ChangeStatusAsync(CurrentUserId, userId, dto);
            return result is null ? NotFound(new { message = "Không tìm thấy thành viên." }) : Ok(result);
        }
        catch (ArgumentException ex)             // trạng thái lạ / thiếu lý do
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)   // tự khóa mình / khóa Admin
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}

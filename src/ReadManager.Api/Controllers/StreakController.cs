using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Streak;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// SPRINT 5 — CHUỖI ĐỌC SÁCH
//   GET  api/me/streak                      → trạng thái chuỗi + mốc + bộ sưu tập (phải đăng nhập)
//   POST api/me/streak/restore              → khôi phục chuỗi vừa mất (tối đa 5 lần, trong 72 giờ)
//   PUT  api/me/streak/appearance           → đổi danh hiệu / khung avatar đang đeo
//   GET  api/me/coupons                     → mã giảm giá dùng được (gửi couponId khi POST api/orders)
//   GET  api/me/streak/notices?take=30      → thông báo về chuỗi
//   PUT  api/me/streak/notices/12/read      → đánh dấu đã đọc
//   PUT  api/me/streak/notices/read-all
//   GET  api/ranking/streak?top=20          → bảng xếp hạng chuỗi (ai cũng xem được)
[ApiController]
public class StreakController : ControllerBase
{
    private readonly IStreakService _streak;

    public StreakController(IStreakService streak) => _streak = streak;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize]
    [HttpGet("api/me/streak")]
    public async Task<ActionResult<StreakDto>> Get() => Ok(await _streak.GetAsync(CurrentUserId));

    [Authorize]
    [HttpPost("api/me/streak/restore")]
    public async Task<ActionResult<StreakDto>> Restore()
    {
        try { return Ok(await _streak.RestoreAsync(CurrentUserId)); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize]
    [HttpPut("api/me/streak/appearance")]
    public async Task<ActionResult<StreakDto>> UpdateAppearance(UpdateAppearanceDto dto)
    {
        try { return Ok(await _streak.UpdateAppearanceAsync(CurrentUserId, dto)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize]
    [HttpGet("api/me/coupons")]
    public async Task<ActionResult<List<CouponDto>>> Coupons() => Ok(await _streak.GetUsableCouponsAsync(CurrentUserId));

    [Authorize]
    [HttpGet("api/me/streak/notices")]
    public async Task<ActionResult<List<StreakNoticeDto>>> Notices([FromQuery] int take = 30)
        => Ok(await _streak.GetNoticesAsync(CurrentUserId, take));

    [Authorize]
    [HttpPut("api/me/streak/notices/{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _streak.MarkNoticeReadAsync(CurrentUserId, id);
        return NoContent();
    }

    [Authorize]
    [HttpPut("api/me/streak/notices/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _streak.MarkAllNoticesReadAsync(CurrentUserId);
        return NoContent();
    }

    [HttpGet("api/ranking/streak")]
    public async Task<ActionResult<List<StreakLeaderboardItemDto>>> Leaderboard([FromQuery] int top = 20)
        => Ok(await _streak.GetLeaderboardAsync(top));
}

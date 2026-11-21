using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Vouchers;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// SPRINT 5 — VOUCHER MUA TRUYỆN
//   GET  api/vouchers/offers                   → danh sách gói voucher có thể đổi bằng xu
//   POST api/vouchers/offers/{offerId}/redeem  → đổi xu lấy voucher (idempotent theo requestId)
//   GET  api/me/vouchers?status=Available      → danh sách voucher của tài khoản đang đăng nhập
[ApiController]
public class VouchersController : ControllerBase
{
    private readonly IVoucherService _vouchers;

    public VouchersController(IVoucherService vouchers) => _vouchers = vouchers;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [AllowAnonymous]
    [HttpGet("api/vouchers/offers")]
    public async Task<ActionResult<List<VoucherOfferDto>>> Offers()
        => Ok(await _vouchers.GetOffersAsync());

    [Authorize]
    [HttpPost("api/vouchers/offers/{offerId:int}/redeem")]
    public async Task<ActionResult<VoucherDto>> Redeem(int offerId, [FromBody] RedeemVoucherDto dto)
    {
        try
        {
            var voucher = await _vouchers.RedeemAsync(CurrentUserId, offerId, dto.RequestId);
            return voucher is null
                ? NotFound(new { message = "Voucher không tồn tại hoặc đã ngừng áp dụng." })
                : Ok(voucher);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("api/me/vouchers")]
    public async Task<ActionResult<List<VoucherDto>>> MyVouchers([FromQuery] string? status = null)
    {
        try
        {
            return Ok(await _vouchers.GetMyVouchersAsync(CurrentUserId, status));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

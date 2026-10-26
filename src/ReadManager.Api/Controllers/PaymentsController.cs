using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Payments;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — THANH TOÁN (PB19, PB21, PB24, PB25)
//   POST api/payments/start                          → người mua bắt đầu 1 lượt thanh toán cho đơn Pending
//   POST api/payments/callback                       → cổng thanh toán báo kết quả (không đăng nhập, xác minh bằng chữ ký)
//   POST api/payments/dev/simulate                   → CHỈ Development: giả lập cổng thanh toán để thử
//   GET  api/admin/payments/needs-recovery           → Admin: đã thu tiền nhưng chưa có quyền đọc
//   POST api/admin/payments/orders/{orderId}/regrant → Admin: chạy lại cấp quyền (không thu thêm tiền)
//
// Mã trả về của callback:
//   200 = đã nhận (kể cả callback lặp)   400 = sai chữ ký / bị từ chối   404 = không có lượt thanh toán
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly IHostEnvironment _env;

    public PaymentsController(IPaymentService payments, IHostEnvironment env)
    {
        _payments = payments;
        _env = env;
    }

    private int? CurrentUserId
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ---------- Người mua ----------
    [Authorize]
    [HttpPost("api/payments/start")]
    public async Task<ActionResult<StartPaymentResultDto>> Start([FromBody] StartPaymentDto dto)
    {
        try
        {
            var result = await _payments.StartAsync(dto.OrderId, CurrentUserId!.Value);
            return result is null
                ? NotFound(new { message = "Không tìm thấy đơn hàng." })
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ---------- Cổng thanh toán → server ----------
    [AllowAnonymous]
    [RequestSizeLimit(16 * 1024)]   // giới hạn dung lượng callback
    [HttpPost("api/payments/callback")]
    public async Task<ActionResult<PaymentCallbackResultDto>> Callback([FromBody] PaymentCallbackDto dto)
        => ToResponse(await _payments.HandleCallbackAsync(dto));

    // ---------- Chỉ để thử (Development) ----------
    [Authorize]
    [HttpPost("api/payments/dev/simulate")]
    public async Task<ActionResult<PaymentCallbackResultDto>> Simulate([FromBody] SimulatePaymentDto dto)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        try
        {
            var result = await _payments.SimulateAsync(
                dto.OrderId, CurrentUserId!.Value, dto.Status, dto.TransactionId, dto.EventId);

            return result is null
                ? NotFound(new { message = "Không tìm thấy đơn hàng." })
                : ToResponse(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ---------- Admin (PB25) ----------
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/payments/needs-recovery")]
    public async Task<ActionResult<PagedResultDto<PaymentRecoveryDto>>> NeedsRecovery(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _payments.GetNeedsRecoveryAsync(page, pageSize));

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/payments/orders/{orderId:int}/regrant")]
    public async Task<ActionResult<RegrantResultDto>> Regrant(int orderId)
    {
        try
        {
            var result = await _payments.RegrantAsync(orderId);
            return result is null
                ? NotFound(new { message = "Không tìm thấy đơn hàng." })
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private ActionResult<PaymentCallbackResultDto> ToResponse(PaymentCallbackResult result)
    {
        var body = new PaymentCallbackResultDto
        {
            Result = result.Outcome.ToString(),
            Reason = result.Reason,
            Message = result.Message,
            OrderId = result.OrderId,
            OrderStatus = result.OrderStatus,
            EntitlementGranted = result.EntitlementGranted
        };

        var status = result.Outcome switch
        {
            PaymentCallbackOutcome.InvalidSignature or PaymentCallbackOutcome.Rejected
                => StatusCodes.Status400BadRequest,
            PaymentCallbackOutcome.PaymentNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status200OK   // đã nhận (kể cả lặp) → cổng thanh toán thôi gửi lại
        };

        return StatusCode(status, body);
    }
}
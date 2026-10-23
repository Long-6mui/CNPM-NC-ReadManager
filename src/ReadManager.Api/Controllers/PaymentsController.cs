using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Orders;
using ReadManager.Api.DTOs.Payments;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — THANH TOÁN (PB19, PB21, PB24, PB25)
//   POST api/payments/callback                       → cổng thanh toán báo kết quả (không đăng nhập, xác minh bằng chữ ký)
//   POST api/orders/{orderId}/cancel                 → người mua hủy đơn đang chờ thanh toán
//   GET  api/admin/payments/needs-recovery           → Admin: giao dịch đã thu tiền nhưng cấp quyền chưa xong
//   POST api/admin/payments/orders/{orderId}/regrant → Admin: chạy lại cấp quyền (không thu thêm tiền)
//   POST api/payments/dev/simulate                   → CHỈ Development: giả lập cổng thanh toán để thử
//
// Mã trả về của callback:
//   200 = đã nhận (kể cả callback lặp)   400 = sai chữ ký / sai số tiền   404 = không có đơn
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly IOrderService _orders;
    private readonly IHostEnvironment _env;

    public PaymentsController(IPaymentService payments, IOrderService orders, IHostEnvironment env)
    {
        _payments = payments;
        _orders = orders;
        _env = env;
    }

    private int? CurrentUserId
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAdmin => User.IsInRole("Admin");

    // ---------- Cổng thanh toán → server ----------
    [AllowAnonymous]
    [HttpPost("api/payments/callback")]
    public async Task<ActionResult<PaymentCallbackResultDto>> Callback([FromBody] PaymentCallbackDto dto)
        => ToResponse(await _payments.HandleCallbackAsync(dto));

    // ---------- Người mua ----------
    [Authorize]
    [HttpPost("api/orders/{orderId:int}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrder(int orderId)
    {
        try
        {
            var order = await _payments.CancelOrderAsync(orderId, CurrentUserId!.Value);
            return order is null
                ? NotFound(new { message = "Không tìm thấy đơn hàng." })
                : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ---------- Admin (PB25) ----------
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/payments/needs-recovery")]
    public async Task<ActionResult<PagedResultDto<PaymentTransactionDto>>> NeedsRecovery(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _payments.GetNeedsRecoveryAsync(page, pageSize));

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/payments/orders/{orderId:int}/regrant")]
    public async Task<ActionResult<RegrantResultDto>> Regrant(int orderId)
    {
        try
        {
            var result = await _payments.RegrantAsync(orderId);
            if (result is null)
                return NotFound(new { message = "Không tìm thấy đơn hàng." });

            return result.Granted ? Ok(result) : StatusCode(StatusCodes.Status500InternalServerError, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ---------- Chỉ để thử (Development) ----------
    [Authorize]
    [HttpPost("api/payments/dev/simulate")]
    public async Task<ActionResult<PaymentCallbackResultDto>> Simulate([FromBody] SimulatePaymentDto dto)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        // Chủ đơn (hoặc Admin) mới được giả lập cho đơn đó
        var order = await _orders.GetByIdAsync(dto.OrderId, CurrentUserId!.Value, IsAdmin);
        if (order is null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });

        try
        {
            var callback = _payments.CreateSignedCallback(order.OrderId, order.Amount, dto.Status, dto.TransactionId);
            // Đi qua ĐÚNG đường xử lý của callback thật (kể cả bước xác minh chữ ký)
            return ToResponse(await _payments.HandleCallbackAsync(callback));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    private ActionResult<PaymentCallbackResultDto> ToResponse(PaymentCallbackResult result)
    {
        var body = new PaymentCallbackResultDto
        {
            Result = result.Outcome.ToString(),
            Message = result.Message,
            OrderId = result.OrderId,
            OrderStatus = result.OrderStatus,
            EntitlementGranted = result.EntitlementGranted
        };

        var status = result.Outcome switch
        {
            PaymentCallbackOutcome.InvalidSignature or PaymentCallbackOutcome.AmountMismatch
                => StatusCodes.Status400BadRequest,
            PaymentCallbackOutcome.OrderNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status200OK   // đã nhận (kể cả lặp) → cổng thanh toán thôi gửi lại
        };

        return StatusCode(status, body);
    }
}
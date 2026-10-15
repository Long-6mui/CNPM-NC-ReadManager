using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Orders;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 3 — ĐƠN HÀNG (PB19, PB21, PB26)
//   GET  api/orders/quote/story/5     → báo giá mua truyện (ai cũng xem được; đăng nhập thì biết đã mua chưa)
//   GET  api/orders/quote/plan/2      → báo giá mua / gia hạn gói
//   POST api/orders                   → tạo đơn (phải đăng nhập)
//   GET  api/orders/12                → chi tiết đơn (chủ đơn hoặc Admin)
//   GET  api/me/orders?status=Paid    → PB26 lịch sử thanh toán của tôi
//   GET  api/admin/orders             → Admin xem tất cả đơn
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAdmin => User.IsInRole("Admin");

    // ----- Báo giá -----
    [HttpGet("api/orders/quote/story/{storyId:int}")]
    public async Task<ActionResult<OrderQuoteDto>> QuoteStory(int storyId)
    {
        var quote = await _orderService.QuoteStoryAsync(storyId, CurrentUserId);
        return quote is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(quote);
    }

    [HttpGet("api/orders/quote/plan/{planId:int}")]
    public async Task<ActionResult<OrderQuoteDto>> QuotePlan(int planId)
    {
        var quote = await _orderService.QuotePlanAsync(planId, CurrentUserId);
        return quote is null ? NotFound(new { message = "Không tìm thấy gói đọc." }) : Ok(quote);
    }

    // ----- Tạo đơn -----
    // body: { "orderType": "Story", "storyId": 5 }  hoặc  { "orderType": "Plan", "planId": 2 }
    [Authorize]
    [HttpPost("api/orders")]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
    {
        try
        {
            var order = await _orderService.CreateAsync(CurrentUserId!.Value, dto);
            return order is null
                ? NotFound(new { message = dto.OrderType == "Story" ? "Không tìm thấy truyện." : "Không tìm thấy gói đọc." })
                : Ok(order);   // 200: có thể là đơn mới hoặc đơn đang chờ thanh toán đã có sẵn
        }
        catch (ArgumentException ex)          // thiếu dữ liệu / không bán
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)  // đã sở hữu
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ----- Chi tiết đơn -----
    [Authorize]
    [HttpGet("api/orders/{orderId:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int orderId)
    {
        var order = await _orderService.GetByIdAsync(orderId, CurrentUserId!.Value, IsAdmin);
        return order is null ? NotFound(new { message = "Không tìm thấy đơn hàng." }) : Ok(order);
    }

    // ----- PB26: lịch sử thanh toán của tôi -----
    [Authorize]
    [HttpGet("api/me/orders")]
    public async Task<ActionResult<PagedResultDto<OrderDto>>> MyOrders(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _orderService.GetMyOrdersAsync(CurrentUserId!.Value, status, page, pageSize));

    // ----- Admin -----
    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/orders")]
    public async Task<ActionResult<PagedResultDto<OrderDto>>> AdminList(
        [FromQuery] string? status, [FromQuery] int? userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _orderService.GetAllAsync(status, userId, page, pageSize));
}

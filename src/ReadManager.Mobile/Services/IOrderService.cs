using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

// Đơn hàng — khớp OrdersController của API (PB19, PB21, PB26)
public interface IOrderService
{
    Task<OrderQuote?> QuoteStoryAsync(int storyId);
    Task<OrderQuote?> QuotePlanAsync(int planId);

    // Giá luôn do server lấy từ database; client chỉ gửi "mua cái gì"
    Task<Order> CreateStoryOrderAsync(int storyId);
    Task<Order> CreatePlanOrderAsync(int planId, int? planVersionId);

    Task<Order> GetOrderAsync(int orderId);
    Task<PagedResult<Order>> GetMyOrdersAsync(string? status, int page, int pageSize);
}

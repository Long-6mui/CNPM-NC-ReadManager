using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public class OrderService : IOrderService
{
    private readonly ApiClient _api;

    public OrderService(ApiClient api) => _api = api;

    public Task<OrderQuote?> QuoteStoryAsync(int storyId) => QuoteAsync($"orders/quote/story/{storyId}");

    public Task<OrderQuote?> QuotePlanAsync(int planId) => QuoteAsync($"orders/quote/plan/{planId}");

    // 404 = không tìm thấy truyện/gói -> trả null cho giao diện tự báo
    private async Task<OrderQuote?> QuoteAsync(string uri)
    {
        try { return await _api.GetAsync<OrderQuote>(uri); }
        catch (ApiException ex) when (ex.StatusCode == 404) { return null; }
    }

    public async Task<Order> CreateStoryOrderAsync(int storyId)
        => await _api.PostAsync<CreateOrderRequest, Order>("orders",
               new CreateOrderRequest { OrderType = "Story", StoryId = storyId })
           ?? throw new ApiException(500, "Không tạo được đơn hàng.");

    public async Task<Order> CreatePlanOrderAsync(int planId, int? planVersionId)
        => await _api.PostAsync<CreateOrderRequest, Order>("orders",
               new CreateOrderRequest { OrderType = "Plan", PlanId = planId, PlanVersionId = planVersionId })
           ?? throw new ApiException(500, "Không tạo được đơn hàng.");

    public async Task<Order> GetOrderAsync(int orderId)
        => await _api.GetAsync<Order>($"orders/{orderId}")
           ?? throw new ApiException(404, "Không tìm thấy đơn hàng.");

    public async Task<PagedResult<Order>> GetMyOrdersAsync(string? status, int page, int pageSize)
    {
        var uri = $"me/orders?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status)) uri += $"&status={Uri.EscapeDataString(status)}";
        return await _api.GetAsync<PagedResult<Order>>(uri) ?? new PagedResult<Order>();
    }
}

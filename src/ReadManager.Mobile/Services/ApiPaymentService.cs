using ReadManager.Mobile.Models;
namespace ReadManager.Mobile.Services;

public class ApiPaymentService(ApiClient api) : IPaymentService
{
    public bool IsAvailable => true;
    public string UnavailableMessage => string.Empty;
    public async Task<PaymentStart> StartAsync(int orderId)
        => await api.PostAsync<StartPaymentRequest, PaymentStart>("payments/start", new() { OrderId = orderId })
           ?? throw new ApiException(500, "Không bắt đầu được thanh toán.");
    public async Task<PaymentOutcome> SimulateAsync(int orderId, string status)
        => await api.PostAsync<object, PaymentOutcome>("payments/dev/simulate", new { orderId, status })
           ?? throw new ApiException(500, "Không nhận được kết quả thanh toán thử.");
}

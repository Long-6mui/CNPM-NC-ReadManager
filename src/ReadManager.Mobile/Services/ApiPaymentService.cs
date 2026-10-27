using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Nối API thanh toán thật của Backend 4 (xem ghi chú ở IPaymentService).
public class ApiPaymentService : IPaymentService
{
    private const string StartUri = "payments/start";
    private const string SimulateUri = "payments/dev/simulate";

    private readonly ApiClient _api;

    public ApiPaymentService(ApiClient api) => _api = api;

    public bool IsAvailable => true;
    public string UnavailableMessage => string.Empty;
    public bool CanSimulate => ApiSettings.AllowPaymentSimulation;

    public async Task<PaymentStart> StartAsync(int orderId)
    {
        // 409 = đơn không còn chờ thanh toán (đã Paid / hết hạn...) -> ApiException mang đúng lời của server
        var started = await _api.PostAsync<StartPaymentRequest, StartPaymentResult>(StartUri, new StartPaymentRequest { OrderId = orderId })
                      ?? throw new ApiException(500, "Không bắt đầu được thanh toán.");

        return new PaymentStart
        {
            PaymentId = started.PaymentId,
            PayUrl = null,   // API chưa có cổng thật nên chưa có link
            Status = "Pending",
            Message = $"Đã tạo lượt thanh toán {started.Amount:N0}₫ cho đơn #{started.OrderId}."
        };
    }

    public async Task<PaymentCallbackResult> SimulateAsync(int orderId, string status)
    {
        try
        {
            return await _api.PostAsync<SimulatePaymentRequest, PaymentCallbackResult>(SimulateUri,
                       new SimulatePaymentRequest { OrderId = orderId, Status = status })
                   ?? throw new ApiException(500, "Không nhận được kết quả từ máy chủ.");
        }
        catch (ApiException ex) when (ex.StatusCode == 404 && ex.Message.Contains("HTTP 404"))
        {
            // API không chạy ở Development nên không có cổng giả lập
            throw new ApiException(404, "Máy chủ này chưa có cổng thanh toán thật và không cho thanh toán thử.");
        }
    }
}

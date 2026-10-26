using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// ============================================================================
// BẢN NỐI API THANH TOÁN (Backend 4) — ĐÃ VIẾT SẴN, ĐANG TẮT.
//
// Khi Backend 4 đẩy PaymentsController lên GitHub:
//   1) Sửa Models/PaymentDtos.cs cho KHỚP DTO thật (DTOs/Payments).
//   2) Sửa đường dẫn StartUri bên dưới cho đúng route thật.
//   3) Đặt ApiSettings.UsePaymentApi = true.
// Đường dẫn và tên trường là DỰ ĐOÁN, chưa phải API thật.
// ============================================================================
public class ApiPaymentService : IPaymentService
{
    // TODO(Backend 4): sửa cho đúng route thật
    private const string StartUri = "payments";

    private readonly ApiClient _api;

    public ApiPaymentService(ApiClient api) => _api = api;

    public bool IsAvailable => true;

    public string UnavailableMessage => string.Empty;

    public async Task<PaymentStart> StartAsync(int orderId)
        => await _api.PostAsync<StartPaymentRequest, PaymentStart>(StartUri, new StartPaymentRequest { OrderId = orderId })
           ?? throw new ApiException(500, "Không bắt đầu được thanh toán.");
}

using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// ============================================================================
// THANH TOÁN — nối với Backend 4 (PaymentsController).
//
// Luồng THẬT của API hiện tại:
//   1) POST api/payments/start {orderId}     -> server mở 1 lượt thanh toán cho đơn Pending
//   2) Cổng thanh toán gọi POST api/payments/callback về SERVER (có chữ ký) -> server mới chuyển đơn sang Paid + cấp quyền đọc
//   3) App KHÔNG tự báo "đã thanh toán": chỉ tải lại đơn (GET api/orders/{id}) để xem kết quả
//
// Cổng thật (VNPay/MoMo) CHƯA được tích hợp ở API, nên chưa có link thanh toán.
// Khi API chạy ở Development, có thêm POST api/payments/dev/simulate để GIẢ LẬP cổng báo kết quả -> app dùng cái này để thử.
// ============================================================================
public interface IPaymentService
{
    bool IsAvailable { get; }
    string UnavailableMessage { get; }

    // Cho phép "thanh toán thử" qua API giả lập (ApiSettings.AllowPaymentSimulation)
    bool CanSimulate { get; }

    // Bắt đầu thanh toán cho 1 đơn đang chờ
    Task<PaymentStart> StartAsync(int orderId);

    // Giả lập cổng báo kết quả: "Success" | "Failed" | "Cancelled"
    Task<PaymentCallbackResult> SimulateAsync(int orderId, string status);
}

public class PendingPaymentService : IPaymentService
{
    public bool IsAvailable => false;
    public bool CanSimulate => false;

    public string UnavailableMessage =>
        "Cổng thanh toán đang được nhóm tích hợp. Đơn hàng của bạn vẫn được giữ trong 30 phút; " +
        "khi cổng thanh toán sẵn sàng, bạn bấm “Tải lại trạng thái” để xem kết quả.";

    public Task<PaymentStart> StartAsync(int orderId)
        => throw new ApiException(501, UnavailableMessage);

    public Task<PaymentCallbackResult> SimulateAsync(int orderId, string status)
        => throw new ApiException(501, UnavailableMessage);
}

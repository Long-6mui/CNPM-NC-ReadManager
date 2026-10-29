using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// API dùng route/DTO hiện tại. Trạng thái thanh toán và quyền đọc do backend quyết định.
public interface IPaymentService
{
    bool IsAvailable { get; }
    string UnavailableMessage { get; }

    // Bắt đầu thanh toán cho 1 đơn đang chờ. Nhận mã giao dịch và số tiền; chưa có URL cổng thật.
    Task<PaymentStart> StartAsync(int orderId);
    Task<PaymentOutcome> SimulateAsync(int orderId, string status);
}

public class PendingPaymentService : IPaymentService
{
    public bool IsAvailable => false;

    public string UnavailableMessage =>
        "Cổng thanh toán đang được nhóm tích hợp (Backend 4 – thanh toán). " +
        "Đơn hàng của bạn vẫn được giữ trong 30 phút; khi cổng thanh toán sẵn sàng, bạn bấm “Tải lại trạng thái” để xem kết quả.";

    public Task<PaymentOutcome> SimulateAsync(int orderId, string status) => throw new ApiException(501, UnavailableMessage);

    public Task<PaymentStart> StartAsync(int orderId)
        => throw new ApiException(501, UnavailableMessage);
}

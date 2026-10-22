using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// ============================================================================
// THANH TOÁN  —  CHỖ NỐI VỚI BACKEND 4 (feature/s2-payments: PaymentsController, PaymentService, PB24–25)
//
// Luồng dự kiến (cổng thanh toán kiểu VNPay/MoMo): app xin "link thanh toán" cho 1 đơn -> mở link bằng trình duyệt ->
// cổng gọi callback về SERVER (server xác minh rồi mới cập nhật đơn thành Paid và cấp quyền) -> app quay lại, tải lại đơn.
// App KHÔNG tự báo "đã thanh toán"; trạng thái luôn lấy từ GET api/orders/{id}.
//
//   - Hiện tại : PendingPaymentService   -> báo "cổng thanh toán đang tích hợp"
//   - Khi API xong: ApiSettings.UsePaymentApi = true -> ApiPaymentService gọi API thật
//     (sửa Models/PaymentDtos.cs và đường dẫn trong ApiPaymentService.cs cho đúng API thật trước khi bật)
// ============================================================================
public interface IPaymentService
{
    bool IsAvailable { get; }
    string UnavailableMessage { get; }

    // Bắt đầu thanh toán cho 1 đơn đang chờ. Trả về link để mở (nếu cổng yêu cầu chuyển trang).
    Task<PaymentStart> StartAsync(int orderId);
}

public class PendingPaymentService : IPaymentService
{
    public bool IsAvailable => false;

    public string UnavailableMessage =>
        "Cổng thanh toán đang được nhóm tích hợp (Backend 4 – thanh toán). " +
        "Đơn hàng của bạn vẫn được giữ trong 30 phút; khi cổng thanh toán sẵn sàng, bạn bấm “Tải lại trạng thái” để xem kết quả.";

    public Task<PaymentStart> StartAsync(int orderId)
        => throw new ApiException(501, UnavailableMessage);
}

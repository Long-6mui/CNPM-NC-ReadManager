namespace ReadManager.Mobile.Models;

// DỰ ĐOÁN hình dạng JSON của API thanh toán (Backend 4). SỬA cho khớp DTO thật (DTOs/Payments) khi API xong.
// (Chỉ dùng trong ApiPaymentService.)

// Body gửi lên khi bắt đầu thanh toán 1 đơn. Số tiền KHÔNG gửi (server lấy từ đơn hàng).
public class StartPaymentRequest
{
    public int OrderId { get; set; }
}

// Kết quả server trả về
public class PaymentStart
{
    public int? PaymentId { get; set; }
    public string? PayUrl { get; set; }      // link cổng thanh toán (mở bằng trình duyệt); null nếu thanh toán không cần chuyển trang
    public string? Status { get; set; }      // Pending | Succeeded | Failed ...
    public string? Message { get; set; }

    public bool HasPayUrl => !string.IsNullOrWhiteSpace(PayUrl);
}

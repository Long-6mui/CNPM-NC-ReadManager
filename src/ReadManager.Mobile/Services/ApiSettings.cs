namespace ReadManager.Mobile.Services;

public static class ApiSettings
{
    // Công tắc nối API quyền đọc của Backend 2 (feature/s2-reading-access).
    // false = tự tính "gói đang dùng / truyện đã mua" từ các đơn đã thanh toán.
    // Khi Backend 2 xong: sửa Models/ReadingAccessDtos.cs + đường dẫn trong ApiReadingAccessService.cs, rồi đặt true.
    public const bool UseReadingAccessApi = false;

    // Công tắc nối API thanh toán của Backend 4 (PaymentsController).
    // false = nút "Thanh toán" báo "cổng thanh toán đang tích hợp".
    // Khi Backend 4 xong: sửa Models/PaymentDtos.cs + đường dẫn trong ApiPaymentService.cs, rồi đặt true.
    public const bool UsePaymentApi = false;

    // Chạy API bằng:  dotnet run --launch-profile http   (cổng 5283, http để emulator khỏi lỗi chứng chỉ).
    // Điện thoại thật: đổi 10.0.2.2 thành IP LAN của máy chạy API, vd http://192.168.1.25:5283/api/
    // - Android Emulator không gọi được "localhost" của máy host -> phải dùng 10.0.2.2
    // - iOS Simulator / Windows thì dùng "localhost" bình thường
    // - Test trên điện thoại thật thì dùng IP LAN của máy chạy API (vd 192.168.1.x)
#if ANDROID
    public const string BaseUrl = "http://10.0.2.2:5283/api/";
#else
    public const string BaseUrl = "http://localhost:5283/api/";
#endif
}

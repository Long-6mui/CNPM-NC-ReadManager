namespace ReadManager.Mobile.Services;

public static class ApiSettings
{
    // API quyền đọc của Backend 2 (GET api/me/library) — ĐÃ NỐI.
    // false = quay về cách tự tính "gói đang dùng / truyện đã mua" từ các đơn đã thanh toán.
    public const bool UseReadingAccessApi = true;

    // API thanh toán của Backend 4 (POST api/payments/start) — ĐÃ NỐI.
    // false = nút "Thanh toán" chỉ báo "cổng thanh toán đang tích hợp".
    public const bool UsePaymentApi = true;

    // Cho phép "thanh toán thử" bằng POST api/payments/dev/simulate (API chỉ có endpoint này khi chạy ở Development).
    // Khi cổng thanh toán thật được tích hợp: đặt false.
    public const bool AllowPaymentSimulation = true;

    // Công tắc nối API "tiếp tục đọc" (ReadingProgress) của Backend 2.
    // API tiến độ đọc của Backend 2 (PUT api/stories/{id}/progress, GET api/me/reading) — ĐÃ NỐI.
    // false = chỉ dùng dữ liệu lưu trên máy (LibraryService).
    public const bool UseReadingProgressApi = false;

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

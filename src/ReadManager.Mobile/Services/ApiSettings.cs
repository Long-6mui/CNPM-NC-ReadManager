namespace ReadManager.Mobile.Services;

public static class ApiSettings
{
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

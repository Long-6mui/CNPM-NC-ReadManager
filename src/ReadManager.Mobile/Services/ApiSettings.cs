namespace ReadManager.Mobile.Services;

public static class ApiSettings
{
    // TODO: đổi lại IP/port đúng chỗ API team Backend đang chạy.
    // - Android Emulator không gọi được "localhost" của máy host -> phải dùng 10.0.2.2
    // - iOS Simulator / Windows thì dùng "localhost" bình thường
    // - Test trên điện thoại thật thì dùng IP LAN của máy chạy API (vd 192.168.1.x)
#if ANDROID
    public const string BaseUrl = "https://10.0.2.2:5001/api/";
#else
    public const string BaseUrl = "https://localhost:5001/api/";
#endif
}

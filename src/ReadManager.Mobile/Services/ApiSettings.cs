using Microsoft.Maui.Storage;

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
    public const bool UseReadingProgressApi = true;

    // Chạy API bằng:  dotnet run --launch-profile http   (cổng 5283, http để emulator khỏi lỗi chứng chỉ).
    // - Android Emulator không gọi được "localhost" của máy host -> mặc định dùng 10.0.2.2
    // - Điện thoại thật: bấm "Địa chỉ máy chủ" ở màn Đăng nhập / Tài khoản rồi nhập IP LAN của máy chạy API
    //   (vd 192.168.1.25:5283). Địa chỉ được nhớ trên máy, không cần sửa code hay build lại.
#if ANDROID
    public const string DefaultBaseUrl = "http://10.0.2.2:5283/api/";
#else
    public const string DefaultBaseUrl = "http://localhost:5283/api/";
#endif

    private const string BaseUrlKey = "api_base_url";

    // Địa chỉ API đang dùng (đọc lúc khởi động app; đổi xong cần mở lại app)
    public static string BaseUrl
    {
        get
        {
            try
            {
                var saved = Preferences.Default.Get(BaseUrlKey, string.Empty);
                return string.IsNullOrWhiteSpace(saved) ? DefaultBaseUrl : saved;
            }
            catch { return DefaultBaseUrl; }
        }
    }

    // null/trống = quay về mặc định
    public static void SetBaseUrl(string? normalizedUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(normalizedUrl)) Preferences.Default.Remove(BaseUrlKey);
            else Preferences.Default.Set(BaseUrlKey, normalizedUrl);
        }
        catch { /* không lưu được: dùng mặc định */ }
    }

    // "192.168.1.25:5283" -> "http://192.168.1.25:5283/api/". Trả null nếu không phải địa chỉ hợp lệ.
    public static string? Normalize(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (!text.Contains("://")) text = "http://" + text;
        text = text.TrimEnd('/');
        if (!text.EndsWith("/api", StringComparison.OrdinalIgnoreCase)) text += "/api";
        text += "/";
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https")
            ? uri.ToString()
            : null;
    }
}

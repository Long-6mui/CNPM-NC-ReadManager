#if ANDROID
using Microsoft.Maui.Platform;
#endif

namespace ReadManager.Mobile.Services;

// Đổi màu thanh trạng thái (giờ, pin...) cho khớp với header của trang.
// Trang chủ: tối như hero "sách bay"; các trang khác: theo màu header (sáng/tối).
// Hiện chỉ làm trên Android; nền tảng khác bỏ qua.
public static class StatusBarHelper
{
    public static void Set(Color color, bool lightIcons)
    {
#if ANDROID
        try
        {
            var window = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
            if (window is null) return;
#pragma warning disable CA1422 // SetStatusBarColor bị đánh dấu cũ từ Android 15 nhưng vẫn chạy ở các bản trước
            window.SetStatusBarColor(color.ToPlatform());
#pragma warning restore CA1422
            var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
            if (controller is not null) controller.AppearanceLightStatusBars = !lightIcons;
        }
        catch
        {
            // không đổi được màu thanh trạng thái thì thôi, không ảnh hưởng app
        }
#endif
    }

    // Header trang chủ (nền tối của hero)
    public static void UseHero() => Set(Color.FromArgb(ThemeService.HeroTop), lightIcons: true);

    // Header các trang còn lại
    public static void UseDefault() => Set(ThemeService.Get("WebHeader"), lightIcons: ThemeService.IsDark);
}

using Microsoft.Maui.Storage;

namespace ReadManager.Mobile.Services;

// CHẾ ĐỘ SÁNG / TỐI (dark mode)
// - Bảng màu của app nằm ở Resources/Styles/WebTheme.xaml (khóa WebBg, WebSurface, WebText...).
// - Các trang dùng {DynamicResource WebXxx} nên khi đổi giá trị khóa ở Application.Resources thì giao diện đổi ngay,
//   không cần mở lại trang.
// - Lựa chọn được lưu trong Preferences nên mở lại app vẫn giữ chế độ đã chọn.
public static class ThemeService
{
    private const string PrefKey = "ui.dark-mode";

    // Màu nền của hero "sách bay" ở trang chủ — dùng chung cho thanh header trang chủ để chuyển màu liền mạch.
    // (Hero luôn tối ở cả hai chế độ, giống web.)
    public const string HeroTop = "#17112C";
    public const string HeroMid = "#0B0D18";
    public const string HeroBottom = "#06080D";

    // Khóa màu -> (giá trị chế độ sáng, giá trị chế độ tối)
    private static readonly Dictionary<string, (string Light, string Dark)> Palette = new()
    {
        ["WebNavy"] = ("#18181B", "#3F3F46"),      // nền avatar
        ["WebRoyal"] = ("#E11D48", "#F43F5E"),     // màu nhấn chính
        ["WebRoyal2"] = ("#BE123C", "#FB7185"),    // liên kết / nhấn đậm
        ["WebAmber"] = ("#EAB308", "#FACC15"),
        ["WebBg"] = ("#F6F5F2", "#0B0B12"),        // nền trang
        ["WebSurface"] = ("#FFFFFF", "#16161F"),   // nền thẻ
        ["WebSurface2"] = ("#EFEDE7", "#1F1F2B"),
        ["WebText"] = ("#18181B", "#F4F4F5"),
        ["WebMuted"] = ("#52525B", "#A1A1AA"),
        ["WebBorder"] = ("#E4E1D9", "#2B2B3A"),
        ["WebSage"] = ("#12A150", "#34D399"),
        ["WebCrimson"] = ("#DC2626", "#F87171"),
        ["WebPill"] = ("#FFE4E9", "#3B1220"),
        ["WebPillText"] = ("#BE123C", "#FDA4AF"),
        ["WebInk2"] = ("#27272A", "#E4E4E7"),

        // Khóa mới cho bản giao diện này
        ["WebField"] = ("#F4F4F5", "#1C1C28"),        // nền ô nhập
        ["WebPlaceholder"] = ("#A1A1AA", "#71717A"),
        ["WebHeader"] = ("#FFFFFF", "#111119"),       // thanh header các trang
        ["WebTabBar"] = ("#FFFFFF", "#111119"),       // menu dưới
        ["WebIconBg"] = ("#F4F2EE", "#1F1F2B"),       // nền nút tròn trên header
        ["WebWarnBg"] = ("#FFF5E5", "#2A1E0E"),       // khung cảnh báo vàng
        ["WebDangerBg"] = ("#FDECEC", "#2A1214"),     // khung cảnh báo đỏ
        ["WebAlertBg"] = ("#FFF4DC", "#2A220E"),
        ["WebStreakDay"] = ("#EFEDE7", "#1F1F2B"),    // ô ngày chưa đọc
        ["WebStreakDayOn"] = ("#FFF1E0", "#3A2410"),  // ô ngày đã đọc
        ["WebChipDark"] = ("#18181B", "#F4F4F5"),     // chip đang chọn (đen ở chế độ sáng, trắng ở chế độ tối)
        ["WebChipDarkText"] = ("#FFFFFF", "#18181B"),
    };

    public static bool IsDark { get; private set; }

    // Các view tạo bằng code (header, hero...) nghe sự kiện này để tự đổi màu
    public static event EventHandler? ThemeChanged;

    private static Application? _app;

    // Gọi một lần trong App() sau InitializeComponent(): ThemeService.Init(this)
    public static void Init(Application app)
    {
        _app = app;
        IsDark = Preferences.Default.Get(PrefKey, false);
        Apply();
    }

    public static void Toggle() => SetDark(!IsDark);

    public static void SetDark(bool dark)
    {
        if (dark == IsDark) return;
        IsDark = dark;
        Preferences.Default.Set(PrefKey, dark);
        Apply();
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    // Lấy màu theo chế độ hiện tại (dùng trong code C#)
    public static Color Get(string key)
        => Palette.TryGetValue(key, out var v) ? Color.FromArgb(IsDark ? v.Dark : v.Light) : Colors.Transparent;

    private static void Apply()
    {
        var app = _app ?? Application.Current;
        if (app is null) return;

        // Đổi cả chế độ hệ thống của app để ô nhập, Picker, hộp thoại... cũng theo màu tối
        app.UserAppTheme = IsDark ? AppTheme.Dark : AppTheme.Light;

        foreach (var (key, value) in Palette)
            app.Resources[key] = Color.FromArgb(IsDark ? value.Dark : value.Light);
    }
}

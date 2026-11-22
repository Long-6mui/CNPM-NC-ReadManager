using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// KIỂU DÁNG KHUNG AVATAR & DANH HIỆU (chỉ là giao diện, quyền dùng do API quyết định)
// - Khung lưu theo MÃ (frame_...), danh hiệu lưu theo TÊN (giống API: UserStreaks.SelectedFrame / SelectedTitle).
// - Đồ người mới (Tier 0): một màu, viền mảnh, không hiệu ứng.
// - Đồ theo chuỗi: càng lên mốc cao càng nhiều màu, viền dày hơn, có hào quang, ký hiệu trang trí;
//   từ mốc 180 ngày khung còn xoay ánh màu.
// Thêm khung / danh hiệu mới: thêm 1 dòng ở đây + 1 dòng trong StreakCatalog của API.
public record FrameStyle(string Code, string Name, int Tier, string[] Colors, double Thickness,
    string? Glow, string? Ornament, bool Spin);

public record TitleStyle(string Name, int Tier, string[] Background, string TextColor, string Icon, string? Glow);

public static class Cosmetics
{
    public static readonly FrameStyle[] Frames =
    {
        // ---- Người mới: đơn giản ----
        new("frame_moc", "Gỗ Mộc", 0, new[] { "#A16207" }, 2.5, null, null, false),
        new("frame_may", "Mây Trắng", 0, new[] { "#94A3B8" }, 2.5, null, null, false),
        new("frame_la_non", "Lá Non", 0, new[] { "#4ADE80" }, 2.5, null, null, false),

        // ---- Theo chuỗi: đẹp dần ----
        new("frame_dom_lua", "Đốm Lửa", 1, new[] { "#FDBA74", "#F97316" }, 3, "#F97316", null, false),
        new("frame_lua_hong", "Lửa Hồng", 2, new[] { "#FDA4AF", "#E11D48", "#FB7185" }, 3, "#E11D48", "🔥", false),
        new("frame_luc_bao", "Lục Bảo", 3, new[] { "#A7F3D0", "#10B981", "#047857", "#6EE7B7" }, 3.5, "#10B981", "🍀", false),
        new("frame_hoang_hon", "Hoàng Hôn", 4, new[] { "#FDE68A", "#F59E0B", "#EF4444", "#8B5CF6" }, 3.5, "#F97316", "🌅", false),
        new("frame_thach_anh", "Thạch Anh Tím", 5, new[] { "#F5D0FE", "#C084FC", "#7C3AED", "#F0ABFC" }, 4, "#A855F7", "🔮", false),
        new("frame_dong", "Ngọn Lửa Đồng", 6, new[] { "#FCD34D", "#D97706", "#B87333", "#FDE68A" }, 4, "#D97706", "🥉", false),
        new("frame_bac", "Ngọn Lửa Bạc", 7, new[] { "#FFFFFF", "#CBD5E1", "#94A3B8", "#F8FAFC", "#64748B" }, 4.5, "#E2E8F0", "⚜️", true),
        new("frame_kim_cuong", "Lửa Kim Cương", 8, new[] { "#67E8F9", "#22D3EE", "#A78BFA", "#F472B6", "#67E8F9" }, 5, "#22D3EE", "💎", true),
    };

    public static readonly TitleStyle[] Titles =
    {
        // ---- Người mới: nền nhạt, chữ màu ----
        new("Tân Độc Giả", 0, new[] { "#E0F2FE" }, "#0369A1", "🌱", null),
        new("Người Mới Đến", 0, new[] { "#F1F5F9" }, "#475569", "👋", null),
        new("Mê Truyện", 0, new[] { "#FCE7F3" }, "#BE185D", "📚", null),

        // ---- Theo chuỗi: nền chuyển màu, chữ trắng, có hào quang ----
        new("Chăm Chỉ Mỗi Ngày", 1, new[] { "#FDBA74", "#F97316" }, "#FFFFFF", "🔥", null),
        new("Kẻ Săn Chương", 3, new[] { "#34D399", "#0D9488" }, "#FFFFFF", "🏹", "#10B981"),
        new("Mọt Sách Tập Sự", 4, new[] { "#F59E0B", "#DC2626" }, "#FFFFFF", "📖", "#F97316"),
        new("Người Giữ Lửa", 5, new[] { "#A855F7", "#E11D48" }, "#FFFFFF", "🕯️", "#A855F7"),
        new("Người Đam Mê", 6, new[] { "#F59E0B", "#B45309", "#78350F" }, "#FEF3C7", "❤️‍🔥", "#D97706"),
        new("Tàng Kinh Các", 7, new[] { "#94A3B8", "#334155", "#0F172A" }, "#F8FAFC", "🏯", "#94A3B8"),
        new("Học Giả", 7, new[] { "#3B82F6", "#1E3A8A", "#7C3AED" }, "#FFFFFF", "🎓", "#6366F1"),
        new("Thần Đồng Gác Sách", 8, new[] { "#22D3EE", "#A855F7", "#EC4899" }, "#FFFFFF", "👑", "#A855F7"),
    };

    public static FrameStyle? Frame(string? code)
        => string.IsNullOrEmpty(code) ? null : Frames.FirstOrDefault(f => f.Code == code);

    public static TitleStyle? Title(string? name)
        => string.IsNullOrEmpty(name) ? null
           : Titles.FirstOrDefault(t => t.Name == name)
             ?? new TitleStyle(name, 1, new[] { "#FFF1E0" }, "#B45309", "🏷", null);   // danh hiệu lạ: kiểu mặc định

    // Màu -> cọ vẽ (1 màu = tô đặc, nhiều màu = chuyển màu chéo)
    public static Brush ToBrush(string[] colors)
    {
        if (colors.Length == 1) return new SolidColorBrush(Color.FromArgb(colors[0]));
        var stops = new GradientStopCollection();
        for (var i = 0; i < colors.Length; i++)
            stops.Add(new GradientStop(Color.FromArgb(colors[i]), i / (float)(colors.Length - 1)));
        return new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 1));
    }

    // Thứ tự hiển thị trong danh sách chọn (người mới trước, rồi theo mốc)
    public static int FrameOrder(string code) => Array.FindIndex(Frames, f => f.Code == code) is var i and >= 0 ? i : 99;
    public static int TitleOrder(string name) => Array.FindIndex(Titles, t => t.Name == name) is var i and >= 0 ? i : 99;
}

// Một khung / danh hiệu trong danh sách chọn ở trang Tài khoản
public partial class CosmeticItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string Kind { get; init; } = "Frame";   // Frame | Title
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Unlocked { get; init; }
    public int Milestone { get; init; }
    public bool Starter { get; init; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectStroke))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(StatusText))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(StatusColor))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectBackground))]
    private bool isSelected;

    public bool Locked => !Unlocked;
    public string Key => Kind == "Title" ? Name : Code;   // API lưu danh hiệu theo tên, khung theo mã

    public Color SelectStroke => IsSelected ? Color.FromArgb("#E11D48") : Colors.Transparent;
    public Color SelectBackground => IsSelected
        ? ReadManager.Mobile.Services.ThemeService.Get("WebPill")
        : Colors.Transparent;
    public string StatusText => IsSelected ? "✓ Đang dùng"
        : Locked ? $"🔒 Chuỗi {Milestone} ngày"
        : Starter ? "Người mới" : $"Mốc {Milestone} ngày";
    public Color StatusColor => IsSelected ? Color.FromArgb("#E11D48")
        : Locked ? Color.FromArgb("#A1A1AA")
        : ReadManager.Mobile.Services.ThemeService.Get("WebMuted");
}

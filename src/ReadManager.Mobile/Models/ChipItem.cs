using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.Models;

// Một chip / tab lọc (thể loại, tiêu chí xếp hạng, tab tủ sách...).
// Màu được TÍNH SẴN theo IsSelected (không dùng DataTrigger + DynamicResource vì MAUI không trả màu cũ
// khi bỏ chọn -> lỗi "bấm nút khác mà nút cũ vẫn đỏ").
public partial class ChipItem : ObservableObject
{
    public ChipItem(string key, string name, int? id = null)
    {
        Key = key;
        Name = name;
        Id = id;
        // Đổi chế độ sáng/tối -> tính lại màu
        ThemeService.ThemeChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    public string Key { get; }   // mã dùng khi gọi API (vd "reads", "week")
    public string Name { get; }  // chữ hiển thị
    public int? Id { get; }      // id thể loại (nếu là chip thể loại; null = "Tất cả")

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background))]
    [NotifyPropertyChangedFor(nameof(Stroke))]
    [NotifyPropertyChangedFor(nameof(Foreground))]
    [NotifyPropertyChangedFor(nameof(AccentText))]
    [NotifyPropertyChangedFor(nameof(FontFamily))]
    [NotifyPropertyChangedFor(nameof(IndicatorColor))]
    private bool isSelected;

    // Chip đặc: đang chọn = nền đỏ chữ trắng; còn lại = nền trắng viền xám
    public Color Background => IsSelected ? ThemeService.Get("WebRoyal") : ThemeService.Get("WebSurface");
    public Color Stroke => IsSelected ? ThemeService.Get("WebRoyal") : ThemeService.Get("WebBorder");
    public Color Foreground => IsSelected ? Colors.White : ThemeService.Get("WebMuted");

    // Tab chữ (gạch chân): đang chọn = chữ đỏ đậm + gạch đỏ
    public Color AccentText => IsSelected ? ThemeService.Get("WebRoyal") : ThemeService.Get("WebMuted");
    public string FontFamily => IsSelected ? "BeVietnamExtraBold" : "BeVietnamMedium";
    public Color IndicatorColor => IsSelected ? ThemeService.Get("WebRoyal") : Colors.Transparent;

    // Số đếm nhỏ cạnh tên tab (vd "Theo dõi 3") — để trống thì ẩn
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    private string badge = string.Empty;

    public bool HasBadge => !string.IsNullOrEmpty(Badge);
}

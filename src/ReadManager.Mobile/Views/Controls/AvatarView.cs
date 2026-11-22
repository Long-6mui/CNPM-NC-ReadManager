using Microsoft.Maui.Controls.Shapes;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.Views.Controls;

// AVATAR CÓ KHUNG (dùng ở Tài khoản, Bình luận, trang Chuỗi, danh sách chọn khung)
//   <ctl:AvatarView Diameter="44" FrameCode="{Binding AvatarFrame}" Url="{Binding AvatarUrl}" Initial="{Binding Initial}" />
//   (Source = ảnh trên máy, Url = ảnh trên máy chủ)
//   Locked="True"  -> xám đi + ổ khóa 🔒 (khung chưa mở khóa)
//   Animated="True" -> khung cao cấp (Bạc, Kim Cương) xoay ánh màu liên tục; hào quang "thở"
public class AvatarView : ContentView
{
    public static readonly BindableProperty DiameterProperty =
        BindableProperty.Create(nameof(Diameter), typeof(double), typeof(AvatarView), 44d, propertyChanged: Changed);
    public static readonly BindableProperty FrameCodeProperty =
        BindableProperty.Create(nameof(FrameCode), typeof(string), typeof(AvatarView), null, propertyChanged: Changed);
    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(nameof(Source), typeof(ImageSource), typeof(AvatarView), null, propertyChanged: Changed);
    // Ảnh từ máy chủ (đường dẫn http...). Rỗng / sai -> hiện chữ cái đầu
    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(AvatarView), null, propertyChanged: Changed);
    public static readonly BindableProperty InitialProperty =
        BindableProperty.Create(nameof(Initial), typeof(string), typeof(AvatarView), "?", propertyChanged: Changed);
    public static readonly BindableProperty LockedProperty =
        BindableProperty.Create(nameof(Locked), typeof(bool), typeof(AvatarView), false, propertyChanged: Changed);
    public static readonly BindableProperty AnimatedProperty =
        BindableProperty.Create(nameof(Animated), typeof(bool), typeof(AvatarView), false, propertyChanged: Changed);

    public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
    public string? FrameCode { get => (string?)GetValue(FrameCodeProperty); set => SetValue(FrameCodeProperty, value); }
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public string? Url { get => (string?)GetValue(UrlProperty); set => SetValue(UrlProperty, value); }
    public string? Initial { get => (string?)GetValue(InitialProperty); set => SetValue(InitialProperty, value); }
    public bool Locked { get => (bool)GetValue(LockedProperty); set => SetValue(LockedProperty, value); }
    public bool Animated { get => (bool)GetValue(AnimatedProperty); set => SetValue(AnimatedProperty, value); }

    private readonly Grid _root = new();
    private readonly Border _ring = new() { StrokeThickness = 0, BackgroundColor = Colors.Transparent };
    private readonly Border _inner = new() { StrokeThickness = 0 };
    private readonly Image _image = new() { Aspect = Aspect.AspectFill };
    private readonly Label _initial = new()
    {
        TextColor = Colors.White, FontFamily = "BeVietnamBold",
        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
    };
    private readonly Label _ornament = new() { HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start };
    private readonly Label _lock = new()
    {
        Text = "🔒", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
    };
    private bool _spinning;

    public AvatarView()
    {
        var face = new Grid();
        face.Children.Add(_initial);
        face.Children.Add(_image);
        _inner.Content = face;

        _root.Children.Add(_ring);
        _root.Children.Add(_inner);
        _root.Children.Add(_ornament);
        _root.Children.Add(_lock);
        Content = _root;
        InputTransparent = false;

        Loaded += (_, _) => { Rebuild(); StartSpin(); };
        Unloaded += (_, _) => StopSpin();
        Rebuild();
    }

    private static void Changed(BindableObject b, object o, object n) => ((AvatarView)b).Rebuild();

    private void Rebuild()
    {
        var size = Math.Max(20, Diameter);
        var style = Cosmetics.Frame(FrameCode);
        var scale = size / 64d;
        var ring = style is null ? 0 : Math.Max(2, style.Thickness * Math.Max(0.7, scale));
        var gap = style is null ? 0 : Math.Max(1.5, 2.5 * scale);

        _root.WidthRequest = size;
        _root.HeightRequest = size;

        // Khung: vòng tròn tô màu (gradient) — phần giữa bị ảnh che, chỉ còn viền
        _ring.WidthRequest = size;
        _ring.HeightRequest = size;
        _ring.StrokeShape = new RoundRectangle { CornerRadius = size / 2 };
        _ring.Background = style is null ? new SolidColorBrush(Colors.Transparent) : Cosmetics.ToBrush(style.Colors);
        _ring.Shadow = style?.Glow is { } glow && !Locked
            ? new Shadow { Brush = new SolidColorBrush(Color.FromArgb(glow)), Radius = (float)(6 + style.Tier * 1.5 * scale), Opacity = 0.75f, Offset = new Point(0, 0) }
            : null;

        // Ảnh / chữ cái đầu
        var innerSize = size - 2 * (ring + gap);
        _inner.WidthRequest = innerSize;
        _inner.HeightRequest = innerSize;
        _inner.HorizontalOptions = LayoutOptions.Center;
        _inner.VerticalOptions = LayoutOptions.Center;
        _inner.StrokeShape = new RoundRectangle { CornerRadius = innerSize / 2 };
        _inner.BackgroundColor = ThemeService.Get("WebNavy");
        if (style is not null)
        {
            // khe hở giữa khung và ảnh cùng màu nền thẻ cho rõ khung
            _inner.Stroke = ThemeService.Get("WebSurface");
            _inner.StrokeThickness = gap;
            _inner.WidthRequest = innerSize + 2 * gap;
            _inner.HeightRequest = innerSize + 2 * gap;
            _inner.StrokeShape = new RoundRectangle { CornerRadius = (innerSize + 2 * gap) / 2 };
        }
        else
        {
            _inner.StrokeThickness = 0;
        }

        _initial.Text = string.IsNullOrWhiteSpace(Initial) ? "?" : Initial;
        _initial.FontSize = Math.Max(10, innerSize * 0.42);
        var source = Source;
        if (source is null && Uri.TryCreate(Url, UriKind.Absolute, out var uri)) source = ImageSource.FromUri(uri);
        _image.Source = source;
        _image.IsVisible = source is not null;

        // Ký hiệu trang trí (🔥 💎 ...) ở góc trên phải
        _ornament.Text = style?.Ornament ?? string.Empty;
        _ornament.FontSize = Math.Max(10, size * 0.26);
        _ornament.Margin = new Thickness(0, -size * 0.06, -size * 0.06, 0);
        _ornament.IsVisible = style?.Ornament is not null && size >= 30;

        // Chưa mở khóa: xám + ổ khóa
        _ring.Opacity = Locked ? 0.35 : 1;
        _inner.Opacity = Locked ? 0.45 : 1;
        _ornament.Opacity = Locked ? 0.4 : 1;
        _lock.IsVisible = Locked;
        _lock.FontSize = Math.Max(12, size * 0.32);

        StopSpin();
        StartSpin();
    }

    // Khung cao cấp: xoay chậm vòng màu tạo cảm giác ánh kim chạy quanh
    private void StartSpin()
    {
        var style = Cosmetics.Frame(FrameCode);
        if (!Animated || Locked || style is null || !style.Spin || _spinning || !IsLoaded) return;
        _spinning = true;
        var spin = new Animation(v => _ring.Rotation = v, 0, 360);
        spin.Commit(_ring, "FrameSpin", 16, 6000, Easing.Linear, (_, _) => _ring.Rotation = 0, () => _spinning);
    }

    private void StopSpin()
    {
        if (!_spinning) return;
        _spinning = false;
        _ring.AbortAnimation("FrameSpin");
        _ring.Rotation = 0;
    }
}

using Microsoft.Maui.Controls.Shapes;
using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Views.Controls;

// HUY HIỆU DANH HIỆU: nhãn bo tròn đổi màu theo từng danh hiệu (xem Models/Cosmetics.cs)
//   <ctl:TitleBadge Title="{Binding Title}" />          (Title rỗng -> tự ẩn)
//   Locked="True" -> xám + 🔒 ; Small="True" -> cỡ nhỏ (cạnh tên trong bình luận)
public class TitleBadge : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TitleBadge), null, propertyChanged: Changed);
    public static readonly BindableProperty LockedProperty =
        BindableProperty.Create(nameof(Locked), typeof(bool), typeof(TitleBadge), false, propertyChanged: Changed);
    public static readonly BindableProperty SmallProperty =
        BindableProperty.Create(nameof(Small), typeof(bool), typeof(TitleBadge), false, propertyChanged: Changed);

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool Locked { get => (bool)GetValue(LockedProperty); set => SetValue(LockedProperty, value); }
    public bool Small { get => (bool)GetValue(SmallProperty); set => SetValue(SmallProperty, value); }

    private readonly Border _pill = new() { StrokeThickness = 0 };
    private readonly Label _icon = new() { VerticalOptions = LayoutOptions.Center };
    private readonly Label _text = new() { FontFamily = "BeVietnamBold", VerticalOptions = LayoutOptions.Center, MaxLines = 1 };

    public TitleBadge()
    {
        _pill.Content = new HorizontalStackLayout { Spacing = 4, Children = { _icon, _text } };
        Content = _pill;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;
        Rebuild();
    }

    private static void Changed(BindableObject b, object o, object n) => ((TitleBadge)b).Rebuild();

    private void Rebuild()
    {
        var style = Cosmetics.Title(Title);
        IsVisible = style is not null;
        if (style is null) return;

        var small = Small;
        _pill.Padding = small ? new Thickness(7, 2) : new Thickness(11, 5);
        _pill.StrokeShape = new RoundRectangle { CornerRadius = small ? 9 : 13 };

        if (Locked)
        {
            _pill.Background = new SolidColorBrush(Color.FromArgb("#E4E4E7"));
            _pill.Shadow = null;
            _icon.Text = "🔒";
            _text.TextColor = Color.FromArgb("#71717A");
        }
        else
        {
            _pill.Background = Cosmetics.ToBrush(style.Background);
            _pill.Shadow = style.Glow is { } glow
                ? new Shadow { Brush = new SolidColorBrush(Color.FromArgb(glow)), Radius = 8 + style.Tier, Opacity = 0.55f, Offset = new Point(0, 2) }
                : null;
            _icon.Text = style.Icon;
            _text.TextColor = Color.FromArgb(style.TextColor);
        }

        _icon.FontSize = small ? 10 : 13;
        _text.FontSize = small ? 10.5 : 13;
        _text.Text = style.Name;
    }
}

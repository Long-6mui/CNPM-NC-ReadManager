using System.Runtime.CompilerServices;

namespace ReadManager.Mobile.Behaviors;

// HIỆU ỨNG CHUYỂN ĐỘNG DÙNG CHUNG (giống hiệu ứng hover / bấm / xuất hiện của web)
//
// Cách dùng trong XAML (khai báo xmlns:fx="clr-namespace:ReadManager.Mobile.Behaviors"):
//   fx:Motion.Press="True"   -> bấm vào thì nhún nhỏ lại rồi bật về; rê chuột (Windows/Mac) thì nổi lên nhẹ như :hover
//   fx:Motion.Reveal="True"  -> khi hiện ra thì mờ dần + trượt lên (các phần tử hiện liền nhau sẽ tự so le)
//
// Các kiểu nút trong WebTheme.xaml đã bật sẵn Press nên mọi nút trong app đều có hiệu ứng.
public static class Motion
{
    // ===================== BẤM / HOVER =====================
    public static readonly BindableProperty PressProperty =
        BindableProperty.CreateAttached("Press", typeof(bool), typeof(Motion), false, propertyChanged: OnPressChanged);

    public static bool GetPress(BindableObject view) => (bool)view.GetValue(PressProperty);
    public static void SetPress(BindableObject view, bool value) => view.SetValue(PressProperty, value);

    // Ghi nhớ view đã gắn hiệu ứng để không gắn hai lần
    private static readonly ConditionalWeakTable<View, object> Attached = new();

    private static void OnPressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View view || newValue is not true) return;
        if (Attached.TryGetValue(view, out _)) return;
        Attached.Add(view, new object());

        // Máy tính (Windows / Mac) có chuột: nhấn-nhả + rê chuột giống :hover / :active của web
        var desktop = DeviceInfo.Current.Idiom == DeviceIdiom.Desktop;

        if (view is Button button)
        {
            button.Pressed += (_, _) => PressDown(button);
            button.Released += (_, _) => PressUp(button);
        }
        else if (desktop)
        {
            var touch = new PointerGestureRecognizer();
            touch.PointerPressed += (_, _) => PressDown(view);
            touch.PointerReleased += (_, _) => PressUp(view);
            view.GestureRecognizers.Add(touch);
        }
        else
        {
            // Điện thoại: chạm vào thẻ / chip thì nhún nhẹ rồi bật về.
            // (Dùng thêm một TapGestureRecognizer thay vì PointerGestureRecognizer để không cản việc cuộn trang.)
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _ = BounceAsync(view);
            view.GestureRecognizers.Add(tap);
        }

        if (!desktop) return;
        var hover = new PointerGestureRecognizer();
        hover.PointerEntered += (_, _) => HoverIn(view);
        hover.PointerExited += (_, _) => HoverOut(view);
        view.GestureRecognizers.Add(hover);
    }

    private static async Task BounceAsync(View view)
    {
        try
        {
            view.AbortAnimation("ScaleTo");
            await view.ScaleTo(0.94, 80, Easing.CubicOut);
            await view.ScaleTo(1, 280, Easing.SpringOut);
        }
        catch { view.Scale = 1; }
    }

    private static void PressDown(View view)
    {
        view.AbortAnimation("ScaleTo");
        _ = view.ScaleTo(0.94, 90, Easing.CubicOut);
    }

    private static void PressUp(View view)
    {
        view.AbortAnimation("ScaleTo");
        _ = view.ScaleTo(1, 260, Easing.SpringOut);
    }

    private static void HoverIn(View view)
    {
        _ = view.ScaleTo(1.03, 160, Easing.CubicOut);
        _ = view.TranslateTo(0, -2, 160, Easing.CubicOut);
    }

    private static void HoverOut(View view)
    {
        _ = view.ScaleTo(1, 200, Easing.CubicOut);
        _ = view.TranslateTo(0, 0, 200, Easing.CubicOut);
    }

    // ===================== XUẤT HIỆN =====================
    public static readonly BindableProperty RevealProperty =
        BindableProperty.CreateAttached("Reveal", typeof(bool), typeof(Motion), false, propertyChanged: OnRevealChanged);

    public static bool GetReveal(BindableObject view) => (bool)view.GetValue(RevealProperty);
    public static void SetReveal(BindableObject view, bool value) => view.SetValue(RevealProperty, value);

    // Thời điểm và độ trễ của lần xuất hiện gần nhất — để các phần tử hiện liền nhau tự so le (stagger)
    private static DateTime _lastReveal = DateTime.MinValue;
    private static int _stagger;

    private static void OnRevealChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement view || newValue is not true) return;

        if (view.IsLoaded) { _ = RevealAsync(view); return; }

        view.Opacity = 0;
        view.Loaded += OnLoaded;

        static void OnLoaded(object? sender, EventArgs e)
        {
            if (sender is not VisualElement v) return;
            v.Loaded -= OnLoaded;
            _ = RevealAsync(v);
        }
    }

    public static async Task RevealAsync(VisualElement view, int extraDelay = 0)
    {
        var now = DateTime.UtcNow;
        _stagger = (now - _lastReveal).TotalMilliseconds < 120 ? Math.Min(_stagger + 45, 420) : 0;
        _lastReveal = now;

        view.Opacity = 0;
        view.TranslationY = 18;
        try
        {
            var delay = _stagger + extraDelay;
            if (delay > 0) await Task.Delay(delay);
            await Task.WhenAll(
                view.FadeTo(1, 380, Easing.CubicOut),
                view.TranslateTo(view.TranslationX, 0, 440, Easing.CubicOut));
        }
        catch
        {
            view.Opacity = 1;
            view.TranslationY = 0;
        }
    }

    // Hiệu ứng vào trang (gọi trong OnAppearing của các trang chính)
    public static async Task PageEnterAsync(VisualElement? content)
    {
        if (content is null) return;
        content.Opacity = 0;
        content.TranslationY = 14;
        try
        {
            await Task.WhenAll(
                content.FadeTo(1, 300, Easing.CubicOut),
                content.TranslateTo(0, 0, 360, Easing.CubicOut));
        }
        catch
        {
            content.Opacity = 1;
            content.TranslationY = 0;
        }
    }

    // Nảy nhẹ (dùng khi bật/tắt chế độ tối, bấm chip...)
    public static async Task PopAsync(VisualElement view)
    {
        try
        {
            await view.ScaleTo(0.8, 90, Easing.CubicIn);
            await view.ScaleTo(1, 320, Easing.SpringOut);
        }
        catch { view.Scale = 1; }
    }
}

using Microsoft.Extensions.DependencyInjection;
using ReadManager.Mobile.Behaviors;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.Views.Controls;

// Thanh header dùng chung cho các trang chính (thay cho thanh tiêu đề mặc định của Shell).
// Trang dùng header này đặt Shell.NavBarIsVisible="False" và gọi Header.RefreshAsync() trong OnAppearing.
public partial class AppHeader : ContentView
{
    // ---------- Thuộc tính dùng trong XAML ----------
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(AppHeader), string.Empty,
            propertyChanged: (b, _, _) => ((AppHeader)b).UpdateTexts());

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(AppHeader), string.Empty,
            propertyChanged: (b, _, _) => ((AppHeader)b).UpdateTexts());

    // true: nền tối liền màu hero "sách bay" (trang chủ)
    public static readonly BindableProperty IsHeroProperty =
        BindableProperty.Create(nameof(IsHero), typeof(bool), typeof(AppHeader), false,
            propertyChanged: (b, _, _) => ((AppHeader)b).ApplyLook());

    // true: hiện logo app bên trái tiêu đề
    public static readonly BindableProperty ShowBrandProperty =
        BindableProperty.Create(nameof(ShowBrand), typeof(bool), typeof(AppHeader), false,
            propertyChanged: (b, _, _) => ((AppHeader)b).LogoBadge.IsVisible = (bool)b.GetValue(ShowBrandProperty));

    // true: dòng phụ hiện "Xin chào, <tên>" khi đã đăng nhập
    public static readonly BindableProperty GreetingProperty =
        BindableProperty.Create(nameof(Greeting), typeof(bool), typeof(AppHeader), false);

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public bool IsHero { get => (bool)GetValue(IsHeroProperty); set => SetValue(IsHeroProperty, value); }
    public bool ShowBrand { get => (bool)GetValue(ShowBrandProperty); set => SetValue(ShowBrandProperty, value); }
    public bool Greeting { get => (bool)GetValue(GreetingProperty); set => SetValue(GreetingProperty, value); }

    private string _greetingText = string.Empty;
    private bool _loggedIn;

    public AppHeader()
    {
        InitializeComponent();
        ApplyLook();

        // Nghe đổi chế độ sáng/tối khi header đang hiển thị
        Loaded += (_, _) => { ThemeService.ThemeChanged -= OnThemeChanged; ThemeService.ThemeChanged += OnThemeChanged; ApplyLook(); };
        Unloaded += (_, _) => ThemeService.ThemeChanged -= OnThemeChanged;
    }

    private static IServiceProvider? Services => IPlatformApplication.Current?.Services;

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyLook();

    // ===================== MÀU SẮC =====================
    private void ApplyLook()
    {
        var dark = ThemeService.IsDark;
        ThemeIcon.Text = dark ? "☀️" : "🌙";

        if (IsHero)
        {
            // Trang chủ: luôn tối, cùng màu đầu dải gradient của hero
            Root.BackgroundColor = Color.FromArgb(ThemeService.HeroTop);
            TitleLabel.TextColor = Colors.White;
            SubtitleLabel.TextColor = Color.FromArgb("#B7C0D6");
            var glass = Color.FromArgb("#1FFFFFFF");
            SearchBtn.BackgroundColor = glass;
            ThemeBtn.BackgroundColor = glass;
            BellBtn.BackgroundColor = glass;
            StreakChip.BackgroundColor = glass;
            StreakLabel.TextColor = Color.FromArgb("#FBBF24");
            SearchIcon.Source = "ic_search_white.png";
            BellBadge.Stroke = Color.FromArgb(ThemeService.HeroTop);
            BottomLine.IsVisible = false;
        }
        else
        {
            Root.BackgroundColor = ThemeService.Get("WebHeader");
            TitleLabel.TextColor = ThemeService.Get("WebText");
            SubtitleLabel.TextColor = ThemeService.Get("WebMuted");
            // trả nền nút về style IconCircle (DynamicResource WebIconBg)
            SearchBtn.ClearValue(BackgroundColorProperty);
            ThemeBtn.ClearValue(BackgroundColorProperty);
            BellBtn.ClearValue(BackgroundColorProperty);
            StreakChip.BackgroundColor = ThemeService.Get("WebPill");
            StreakLabel.TextColor = ThemeService.Get("WebPillText");
            SearchIcon.Source = dark ? "ic_search_white.png" : "ic_search_ink.png";
            BellBadge.Stroke = ThemeService.Get("WebHeader");
            BottomLine.IsVisible = true;
            BottomLine.Color = ThemeService.Get("WebBorder");
        }
    }

    private void UpdateTexts()
    {
        TitleLabel.Text = Title;
        var sub = !string.IsNullOrWhiteSpace(_greetingText) ? _greetingText : Subtitle;
        SubtitleLabel.Text = sub;
        SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(sub);
    }

    // ===================== DỮ LIỆU (gọi trong OnAppearing của trang) =====================
    public async Task RefreshAsync()
    {
        ApplyLook();
        if (IsHero) StatusBarHelper.UseHero(); else StatusBarHelper.UseDefault();

        var auth = Services?.GetService<IAuthService>();
        try { _loggedIn = auth is not null && (auth.CurrentUser is not null || await auth.IsLoggedInAsync()); }
        catch { _loggedIn = auth?.CurrentUser is not null; }

        var user = auth?.CurrentUser;
        _greetingText = Greeting && _loggedIn && user is not null
            ? $"Xin chào, {(string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName)} 👋"
            : string.Empty;
        UpdateTexts();

        if (!_loggedIn)
        {
            StreakChip.IsVisible = false;
            BellBadge.IsVisible = false;
            return;
        }

        await Task.WhenAll(RefreshStreakAsync(), RefreshBellAsync());
    }

    private async Task RefreshStreakAsync()
    {
        try
        {
            var streak = Services?.GetService<IStreakService>();
            if (streak is null) return;
            var info = await streak.GetAsync();
            var icon = info.IconTier == "darkred" ? "❤️‍🔥" : "🔥";
            var warn = info.CurrentStreak > 0 && !info.ReadToday ? "!" : string.Empty;
            StreakLabel.Text = $"{icon} {info.CurrentStreak}{warn}";
            StreakChip.IsVisible = true;
        }
        catch { StreakChip.IsVisible = false; }
    }

    private async Task RefreshBellAsync()
    {
        try
        {
            var community = Services?.GetService<ICommunityService>();
            if (community is null) return;
            var unread = await community.GetUnreadCountAsync();
            BellCount.Text = unread > 99 ? "99+" : unread.ToString();
            BellBadge.IsVisible = unread > 0;
        }
        catch { BellBadge.IsVisible = false; }
    }

    // ===================== BẤM NÚT =====================
    private async void OnSearchTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(SearchPage));

    private async void OnThemeTapped(object? sender, TappedEventArgs e)
    {
        // xoay biểu tượng + nảy nhẹ rồi đổi chế độ
        _ = ThemeIcon.RotateTo(ThemeIcon.Rotation + 360, 450, Easing.CubicInOut);
        await Motion.PopAsync(ThemeBtn);
        ThemeService.Toggle();
        if (IsHero) StatusBarHelper.UseHero(); else StatusBarHelper.UseDefault();
    }

    private async void OnBellTapped(object? sender, TappedEventArgs e)
    {
        if (_loggedIn) await Shell.Current.GoToAsync(nameof(NotificationsPage));
        else await Shell.Current.GoToAsync("//LoginPage");
    }

    private async void OnStreakTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(StreakPage));
}

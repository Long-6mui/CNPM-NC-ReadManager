using Microsoft.Extensions.DependencyInjection;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;
using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class StoryListPage : ContentPage
{
    private readonly StoryListViewModel _viewModel;

    public StoryListPage(StoryListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
        _ = RefreshBellAsync();
        _ = RefreshFlameAsync();
    }

    // Chuông thông báo (Sprint 3): hiện số thông báo chưa đọc nếu đã đăng nhập
    private async Task RefreshBellAsync()
    {
        try
        {
            var services = Handler?.MauiContext?.Services;
            var auth = services?.GetService<IAuthService>();
            var community = services?.GetService<ICommunityService>();
            if (auth is null || community is null || auth.CurrentUser is null) { BellItem.Text = "🔔"; return; }
            var unread = await community.GetUnreadCountAsync();
            BellItem.Text = unread > 0 ? $"🔔 {unread}" : "🔔";
        }
        catch { BellItem.Text = "🔔"; }
    }

    // Sprint 5: chip 🔥 số ngày chuỗi (cam; từ mốc 90 ngày đổi sang đỏ đậm — ToolbarItem không đổi màu được nên thêm biểu tượng ❤️‍🔥)
    private async Task RefreshFlameAsync()
    {
        try
        {
            var services = Handler?.MauiContext?.Services;
            var auth = services?.GetService<IAuthService>();
            var streak = services?.GetService<IStreakService>();
            if (auth is null || streak is null || auth.CurrentUser is null) { FlameItem.Text = "🔥"; return; }
            var info = await streak.GetAsync();
            var icon = info.IconTier == "darkred" ? "❤️‍🔥" : "🔥";
            var warn = info.CurrentStreak > 0 && !info.ReadToday ? "!" : string.Empty;
            FlameItem.Text = $"{icon} {info.CurrentStreak}{warn}";
        }
        catch { FlameItem.Text = "🔥"; }
    }

    private async void OnFlameClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(StreakPage));

    private async void OnBellClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(NotificationsPage));
}

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
        _ = LoadRecommendationsAsync();
    }

    private int _recommendationLoad;
    private async Task LoadRecommendationsAsync()
    {
        var load = ++_recommendationLoad;
        Recommendations.Clear();
        Recommendations.IsVisible = false;
        var services = Handler?.MauiContext?.Services;
        var auth = services?.GetService<IAuthService>();
        if (services is null || auth is null) return;
        try
        {
            if (auth.CurrentUser is null && !await auth.IsLoggedInAsync()) return;
            var userId = auth.CurrentUser?.UserId;
            var stories = await services.GetRequiredService<ReaderActivityService>().Recommendations();
            if (load != _recommendationLoad || userId != auth.CurrentUser?.UserId) return;
            if (stories is not { Count: > 0 }) return;
            Recommendations.Add(new Label { Text = "Dành cho bạn", FontSize = 20, FontAttributes = FontAttributes.Bold });
            foreach (var story in stories)
                Recommendations.Add(HeartRankingPage.StoryButton(story, story.Title));
            Recommendations.IsVisible = true;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
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

    private async void OnBellClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(NotificationsPage));
}

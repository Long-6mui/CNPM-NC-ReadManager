using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Sprint 3 — Truyện đang theo dõi (GET api/me/follows)
public partial class FollowedStoriesViewModel : BaseViewModel
{
    private readonly ICommunityService _community;
    private readonly IAuthService _auth;

    public ObservableCollection<FollowedStory> Items { get; } = new();

    [ObservableProperty] private bool isLoggedIn = true;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private string countText = string.Empty;

    public FollowedStoriesViewModel(ICommunityService community, IAuthService auth)
    {
        _community = community;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try { IsLoggedIn = await _auth.IsLoggedInAsync(); }
        catch { IsLoggedIn = _auth.CurrentUser is not null; }

        if (!IsLoggedIn)
        {
            Items.Clear();
            ShowEmpty = false;
            CountText = string.Empty;
            return;
        }

        await RunSafeAsync(async () =>
        {
            var list = await _community.GetMyFollowsAsync();
            Items.Clear();
            foreach (var s in list.OrderByDescending(x => x.FollowedAt)) Items.Add(s);
            ShowEmpty = Items.Count == 0;
            CountText = Items.Count > 0 ? $"{Items.Count} truyện" : string.Empty;
        });
    }

    [RelayCommand]
    private async Task OpenAsync(FollowedStory? story)
    {
        if (story is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object> { { "id", story.StoryId } });
    }

    [RelayCommand]
    private async Task UnfollowAsync(FollowedStory? story)
    {
        if (story is null) return;
        var ok = await Shell.Current.DisplayAlert("Bỏ theo dõi", $"Bỏ theo dõi “{story.Title}”?", "Bỏ theo dõi", "Hủy");
        if (!ok) return;

        await RunSafeAsync(async () =>
        {
            await _community.SetFollowAsync(story.StoryId, false);
            Items.Remove(story);
            ShowEmpty = Items.Count == 0;
            CountText = Items.Count > 0 ? $"{Items.Count} truyện" : string.Empty;
        });
    }

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");
}

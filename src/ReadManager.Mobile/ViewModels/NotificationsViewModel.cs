using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Sprint 3 — "Thông báo của tôi" (giống Views/Notifications/Index.cshtml của web)
public partial class NotificationsViewModel : BaseViewModel
{
    private const int PageSize = 20;

    private readonly ICommunityService _community;
    private readonly IAuthService _auth;
    private int _page = 1;

    public ObservableCollection<AppNotification> Items { get; } = new();

    [ObservableProperty] private bool isLoggedIn = true;
    [ObservableProperty] private bool hasMore;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private bool hasUnread;
    [ObservableProperty] private string countText = string.Empty;

    public NotificationsViewModel(ICommunityService community, IAuthService auth)
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
            HasMore = false;
            HasUnread = false;
            CountText = string.Empty;
            return;
        }

        _page = 1;
        await RunSafeAsync(async () =>
        {
            var list = await _community.GetNotificationsAsync(_page, PageSize);
            Items.Clear();
            foreach (var n in list.Items) Items.Add(n);
            HasMore = _page < list.TotalPages;
            ShowEmpty = Items.Count == 0;
            HasUnread = list.UnreadCount > 0;
            CountText = list.UnreadCount > 0 ? $"{list.UnreadCount} thông báo chưa đọc" : "Bạn đã đọc hết thông báo";
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore) return;
        await RunSafeAsync(async () =>
        {
            var next = _page + 1;
            var list = await _community.GetNotificationsAsync(next, PageSize);
            foreach (var n in list.Items) Items.Add(n);
            _page = next;
            HasMore = _page < list.TotalPages;
        });
    }

    // Bấm thông báo -> đánh dấu đã đọc rồi mở đúng chương / truyện
    [RelayCommand]
    private async Task OpenAsync(AppNotification? n)
    {
        if (n is null) return;

        if (!n.IsRead)
        {
            try
            {
                await _community.MarkReadAsync(n.NotificationId);
                n.IsRead = true;
                HasUnread = Items.Any(i => i.IsUnread);
            }
            catch { /* lỗi mạng: vẫn mở nội dung */ }
        }

        if (n.ChapterId.HasValue)
            await Shell.Current.GoToAsync(nameof(Views.ChapterReaderPage), new Dictionary<string, object> { { "chapterId", n.ChapterId.Value } });
        else
            await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object> { { "id", n.StoryId } });
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await RunSafeAsync(async () =>
        {
            await _community.MarkAllReadAsync();
            foreach (var n in Items) n.IsRead = true;
            HasUnread = false;
            CountText = "Bạn đã đọc hết thông báo";
        });
    }

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");
}

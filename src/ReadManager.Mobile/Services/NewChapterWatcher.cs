using Microsoft.Maui.Storage;
using ReadManager.Mobile.Services.Auth;
#if ANDROID
using Android.App;
using Android.Content;
using AndroidX.Core.App;
#endif

namespace ReadManager.Mobile.Services;

// BÁO CHƯƠNG MỚI CHO NGƯỜI THEO DÕI
// - Máy chủ đã tự tạo thông báo cho người theo dõi khi truyện ra chương mới (NotificationService.SyncChaptersAsync).
// - Lớp này cứ 45 giây hỏi lại danh sách thông báo (khi đã đăng nhập). Có thông báo chương mới chưa từng thấy:
//     + App đang mở  -> hiện hộp thoại "📢 ... vừa ra Chương N" với nút "Đọc ngay" mở thẳng chương đó.
//     + App chạy nền -> (Android) hiện thông báo hệ thống trên thanh trạng thái.
// - Lần chạy đầu của mỗi tài khoản chỉ ghi nhớ mốc, không bật thông báo cũ (thông báo cũ vẫn xem ở 🔔).
public class NewChapterWatcher
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(45);

    private readonly ICommunityService _community;
    private readonly IAuthService _auth;
    private IDispatcherTimer? _timer;
    private bool _checking;

    // App đang hiện trên màn hình hay chạy nền (App.xaml.cs cập nhật theo Window.Stopped / Resumed)
    public static bool InForeground { get; set; } = true;

    // Có thông báo mới -> header cập nhật số trên chuông
    public static event EventHandler? NotificationsChanged;

    public NewChapterWatcher(ICommunityService community, IAuthService auth)
    {
        _community = community;
        _auth = auth;
    }

    public void Start(IDispatcher dispatcher)
    {
        if (_timer is not null) return;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = Interval;
        _timer.Tick += async (_, _) => await CheckAsync();
        _timer.Start();
        _ = RequestPermissionAsync();
    }

    public async Task CheckAsync()
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var user = _auth.CurrentUser;
            if (user is null) return;

            var list = await _community.GetNotificationsAsync(1, 10);
            var chapterNotices = list.Items.Where(n => n.ChapterId is not null).ToList();
            if (chapterNotices.Count == 0) return;

            var key = $"notify.last-seen.{user.UserId}";
            var lastSeen = Preferences.Default.Get(key, -1);
            var maxId = chapterNotices.Max(n => n.NotificationId);
            Preferences.Default.Set(key, Math.Max(lastSeen, maxId));
            if (lastSeen < 0) return;   // lần đầu: chỉ ghi mốc

            var fresh = chapterNotices
                .Where(n => n.NotificationId > lastSeen && !n.IsRead)
                .OrderByDescending(n => n.NotificationId)
                .ToList();
            if (fresh.Count == 0) return;

            NotificationsChanged?.Invoke(this, EventArgs.Empty);

            if (!InForeground)
            {
                foreach (var n in fresh.Take(3)) ShowSystemNotification(n.NotificationId, "📢 Chương mới", n.Message);
                return;
            }

            var newest = fresh[0];
            var more = fresh.Count > 1 ? $"\n\n(+{fresh.Count - 1} thông báo chương mới khác ở 🔔)" : string.Empty;
            var page = Shell.Current?.CurrentPage;
            if (page is null) return;
            var readNow = await page.DisplayAlert("📢 Truyện bạn theo dõi có chương mới", newest.Message + more, "Đọc ngay", "Để sau");
            if (!readNow || newest.ChapterId is not int chapterId) return;

            try { await _community.MarkReadAsync(newest.NotificationId); } catch { /* không quan trọng */ }
            NotificationsChanged?.Invoke(this, EventArgs.Empty);
            await Shell.Current!.GoToAsync(nameof(Views.ChapterReaderPage), new Dictionary<string, object>
            {
                { "chapterId", chapterId }
            });
        }
        catch
        {
            // mất mạng / hết phiên: lần sau hỏi lại
        }
        finally
        {
            _checking = false;
        }
    }

    // Android 13+ cần người dùng cho phép hiện thông báo
    private static async Task RequestPermissionAsync()
    {
#if ANDROID
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch { /* người dùng từ chối: vẫn còn hộp thoại trong app + chuông 🔔 */ }
#else
        await Task.CompletedTask;
#endif
    }

    private static void ShowSystemNotification(int id, string title, string text)
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            const string channelId = "new-chapters";
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
                manager?.CreateNotificationChannel(new NotificationChannel(channelId, "Chương mới", NotificationImportance.Default));
            }

            // Bấm vào thông báo -> mở lại app
            var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
            var builder = new NotificationCompat.Builder(context, channelId)
                .SetSmallIcon(context.ApplicationInfo?.Icon ?? 0)
                .SetContentTitle(title)
                .SetContentText(text)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(text))
                .SetAutoCancel(true);
            if (launch is not null)
                builder.SetContentIntent(PendingIntent.GetActivity(context, id, launch,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent));

            NotificationManagerCompat.From(context).Notify(id, builder.Build());
        }
        catch { /* chưa được cấp quyền thông báo */ }
#endif
    }
}

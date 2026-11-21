using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "Tài khoản" — theo trang Views/Account/Profile.cshtml của web:
// thẻ thông tin + ảnh đại diện, menu (thông báo, sở thích đọc, gói đọc, lịch sử thanh toán), thông tin cá nhân,
// đổi mật khẩu và nút Đăng xuất ở cuối trang.
// (Xu + chuỗi đọc đã chuyển sang tab Khám phá; truyện theo dõi / đã thích / đã xem nằm ở tab Tủ sách.)
public partial class AccountViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ICommunityService _community;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    private bool isLoggedIn;

    [ObservableProperty] private string displayName = "Thành viên";
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string userIdText = string.Empty;
    [ObservableProperty] private string roleText = "Thành viên";
    [ObservableProperty] private string initial = "?";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAvatar))]
    private ImageSource? avatarSource;

    [ObservableProperty] private bool hasAvatar;

    // Sprint 3: thông báo chưa đọc + trạng thái tài khoản (bị hạn chế / khóa)
    [ObservableProperty] private string notificationsText = string.Empty;   // "3 mới" cạnh dòng Thông báo
    [ObservableProperty] private string statusBanner = string.Empty;
    [ObservableProperty] private bool hasStatusBanner;

    // Đổi mật khẩu
    [ObservableProperty] private string currentPassword = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private string passwordMessage = string.Empty;

    public bool IsGuest => !IsLoggedIn;
    public bool HasNoAvatar => !HasAvatar;
    [RelayCommand] private Task EditGenresAsync() => Shell.Current.GoToAsync(nameof(Views.ReaderGenresPage));

    // ----- Khung avatar & danh hiệu (đổi ngay tại trang Tài khoản) -----
    private readonly IStreakService _streak;
    public System.Collections.ObjectModel.ObservableCollection<CosmeticItem> Frames { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<CosmeticItem> Titles { get; } = new();
    [ObservableProperty] private string? selectedFrame;
    [ObservableProperty] private string? selectedTitle;
    [ObservableProperty] private string cosmeticSummary = string.Empty;
    private int _longestStreak;

    // Trang nảy avatar khi vừa đổi khung / danh hiệu
    public event EventHandler? CosmeticChanged;

    public AccountViewModel(IAuthService authService, ICommunityService community, IStreakService streak)
    {
        _authService = authService;
        _community = community;
        _streak = streak;
    }

    private async Task LoadCosmeticsAsync()
    {
        try
        {
            ApplyStreak(await _streak.GetAsync());
        }
        catch { /* chưa có API chuỗi / mất mạng: ẩn phần chọn */ }
    }

    private void ApplyStreak(StreakInfo info)
    {
        _longestStreak = info.LongestStreak;
        SelectedFrame = info.SelectedFrame;
        SelectedTitle = info.SelectedTitle;

        Frames.Clear();
        foreach (var r in info.Collection.Where(r => r.Kind == "Frame").OrderBy(r => Cosmetics.FrameOrder(r.Code)))
            Frames.Add(new CosmeticItem { Kind = "Frame", Code = r.Code, Name = r.Name, Unlocked = r.Unlocked,
                Milestone = r.Milestone, Starter = r.Starter, IsSelected = r.Code == info.SelectedFrame });
        Titles.Clear();
        foreach (var r in info.Collection.Where(r => r.Kind == "Title").OrderBy(r => Cosmetics.TitleOrder(r.Name)))
            Titles.Add(new CosmeticItem { Kind = "Title", Code = r.Code, Name = r.Name, Unlocked = r.Unlocked,
                Milestone = r.Milestone, Starter = r.Starter, IsSelected = r.Name == info.SelectedTitle });

        var open = Frames.Count(f => f.Unlocked) + Titles.Count(t => t.Unlocked);
        CosmeticSummary = $"Đã mở {open}/{Frames.Count + Titles.Count} · chuỗi dài nhất {info.LongestStreak} ngày";
    }

    // Bấm một khung / danh hiệu: chưa mở -> báo cần chuỗi bao nhiêu ngày; đang dùng -> bỏ đeo; còn lại -> đeo
    [RelayCommand]
    private async Task PickCosmeticAsync(CosmeticItem? item)
    {
        if (item is null) return;
        if (item.Locked)
        {
            var go = await Shell.Current.DisplayAlert("🔒 Chưa mở khóa",
                $"Giữ chuỗi đọc {item.Milestone} ngày liên tiếp để mở khóa “{item.Name}”.\nChuỗi dài nhất của bạn: {_longestStreak} ngày.",
                "Xem chuỗi", "Đóng");
            if (go) await Shell.Current.GoToAsync(nameof(Views.StreakPage));
            return;
        }

        var isFrame = item.Kind == "Frame";
        var newFrame = isFrame ? (item.IsSelected ? null : item.Code) : SelectedFrame;
        var newTitle = isFrame ? SelectedTitle : (item.IsSelected ? null : item.Name);
        try
        {
            ApplyStreak(await _streak.EquipAsync(newTitle, newFrame));
            CosmeticChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Chưa đổi được", ex.Message, "OK");
        }
    }

    partial void OnHasAvatarChanged(bool value) => OnPropertyChanged(nameof(HasNoAvatar));

    // Gọi mỗi lần tab hiện ra để cập nhật sau khi đăng nhập/đăng xuất/xem truyện
    [RelayCommand]
    private async Task LoadAsync()
    {
        try { IsLoggedIn = await _authService.IsLoggedInAsync(); }
        catch { IsLoggedIn = _authService.CurrentUser is not null; } // mất mạng

        var user = _authService.CurrentUser;
        if (!IsLoggedIn || user is null) return;

        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
        DisplayName = name;
        Email = user.Email;
        UserIdText = user.UserId.ToString();
        RoleText = "Thành viên";
        Initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();

        LoadAvatar(user.UserId);
        await LoadCosmeticsAsync();

        var restricted = user.IsRestricted || SessionFlags.AccountRestricted;
        StatusBanner = user.IsRestricted
            ? user.StatusMessage
            : restricted ? "Tài khoản của bạn đang bị hạn chế: vẫn đọc được, nhưng không thể bình luận, báo cáo hay theo dõi truyện. Liên hệ quản trị viên nếu cần hỗ trợ." : string.Empty;
        HasStatusBanner = restricted;

        try
        {
            var unread = await _community.GetUnreadCountAsync();
            NotificationsText = unread > 0 ? $"{unread} mới" : string.Empty;
        }
        catch { NotificationsText = string.Empty; }
    }

    // "Đổi ảnh đại diện" — ảnh lưu trên thiết bị này (giống web: lưu trên trình duyệt)
    [RelayCommand]
    private async Task ChangeAvatarAsync()
    {
        var user = _authService.CurrentUser;
        if (user is null) return;

        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Chọn ảnh đại diện",
                FileTypes = FilePickerFileType.Images
            });
            if (picked is null) return;

            var oldPath = Preferences.Default.Get(AvatarKey(user.UserId), string.Empty);

            // tên file mới mỗi lần để ảnh cũ không bị giữ trong bộ nhớ đệm
            var path = Path.Combine(FileSystem.AppDataDirectory, $"avatar_{user.UserId}_{DateTime.UtcNow.Ticks}.img");
            await using (var source = await picked.OpenReadAsync())
            await using (var target = File.Create(path))
            {
                await source.CopyToAsync(target);
            }

            Preferences.Default.Set(AvatarKey(user.UserId), path);
            if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath)) File.Delete(oldPath);

            LoadAvatar(user.UserId);
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlert("Ảnh đại diện", "Không đặt được ảnh đại diện. Vui lòng thử ảnh khác.", "Đóng");
        }
    }

    // Web: API đổi mật khẩu chưa có -> chỉ kiểm tra form rồi báo như web
    [RelayCommand]
    private void ChangePassword()
    {
        if (string.IsNullOrEmpty(CurrentPassword) || string.IsNullOrEmpty(NewPassword) || NewPassword.Length < 6)
            PasswordMessage = "Mật khẩu mới tối thiểu 6 ký tự.";
        else if (NewPassword != ConfirmPassword)
            PasswordMessage = "Xác nhận mật khẩu không khớp.";
        else
        {
            PasswordMessage = "Đổi mật khẩu cần API từ Backend 2 (chưa có), nên mật khẩu chưa được thay đổi.";
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }
    }

    // Sprint 2: gói đọc, mua truyện, lịch sử thanh toán
    [RelayCommand]
    private async Task GoToPlansAsync() => await Shell.Current.GoToAsync(nameof(Views.PlansPage));

    [RelayCommand]
    private async Task GoToPaidStoriesAsync() => await Shell.Current.GoToAsync(nameof(Views.SearchPage));

    [RelayCommand]
    private async Task GoToOrderHistoryAsync() => await Shell.Current.GoToAsync(nameof(Views.OrderHistoryPage));


    // Sprint 3: thông báo, truyện đang theo dõi
    [RelayCommand]
    private async Task GoToNotificationsAsync() => await Shell.Current.GoToAsync(nameof(Views.NotificationsPage));


    [RelayCommand]
    private Task OpenServerSettingsAsync() => ServerSettings.PromptAsync();

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!await Shell.Current.DisplayAlert("Đăng xuất", "Bạn muốn đăng xuất khỏi tài khoản này?", "Đăng xuất", "Ở lại")) return;
        await _authService.LogoutAsync();
        SessionFlags.AccountRestricted = false;
        CurrentPassword = NewPassword = ConfirmPassword = PasswordMessage = string.Empty;
        await LoadAsync();
    }

    // ---------------------------------------------------------------

    private static string AvatarKey(int userId) => $"avatar.{userId}";

    private void LoadAvatar(int userId)
    {
        var path = Preferences.Default.Get(AvatarKey(userId), string.Empty);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            AvatarSource = ImageSource.FromFile(path);
            HasAvatar = true;
        }
        else
        {
            AvatarSource = null;
            HasAvatar = false;
        }
    }
}

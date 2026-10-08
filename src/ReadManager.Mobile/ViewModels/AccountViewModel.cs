using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "Tài khoản" — theo trang Views/Account/Profile.cshtml của web:
// thẻ thông tin + ảnh đại diện, gói đọc, truyện đã xem, truyện đã thích, thông tin cá nhân, đổi mật khẩu.
public partial class AccountViewModel : BaseViewModel
{
    private const int PreviewCount = 3;

    private readonly IAuthService _authService;
    private readonly ILibraryService _library;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    private bool isLoggedIn;

    [ObservableProperty] private bool isAdmin;
    [ObservableProperty] private string displayName = "Thành viên";
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string userIdText = string.Empty;
    [ObservableProperty] private string roleText = "Thành viên";
    [ObservableProperty] private string initial = "?";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAvatar))]
    private ImageSource? avatarSource;

    [ObservableProperty] private bool hasAvatar;

    [ObservableProperty] private bool hasHistory;
    [ObservableProperty] private bool hasFavorites;

    // Đổi mật khẩu
    [ObservableProperty] private string currentPassword = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private string passwordMessage = string.Empty;

    public bool IsGuest => !IsLoggedIn;
    public bool HasNoAvatar => !HasAvatar;
    public bool HasNoHistory => !HasHistory;
    public bool HasNoFavorites => !HasFavorites;

    public ObservableCollection<LibraryEntry> HistoryPreview { get; } = new();
    public ObservableCollection<LibraryEntry> FavoritePreview { get; } = new();

    public AccountViewModel(IAuthService authService, ILibraryService library)
    {
        _authService = authService;
        _library = library;
    }

    partial void OnHasAvatarChanged(bool value) => OnPropertyChanged(nameof(HasNoAvatar));
    partial void OnHasHistoryChanged(bool value) => OnPropertyChanged(nameof(HasNoHistory));
    partial void OnHasFavoritesChanged(bool value) => OnPropertyChanged(nameof(HasNoFavorites));

    // Gọi mỗi lần tab hiện ra để cập nhật sau khi đăng nhập/đăng xuất/xem truyện
    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        try { IsLoggedIn = await _authService.IsLoggedInAsync(); }
        catch { IsLoggedIn = _authService.CurrentUser is not null; } // mất mạng

        var user = _authService.CurrentUser;
        if (!IsLoggedIn || user is null)
        {
            HistoryPreview.Clear();
            FavoritePreview.Clear();
            HasHistory = false;
            HasFavorites = false;
            return;
        }

        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
        DisplayName = name;
        Email = user.Email;
        UserIdText = user.UserId.ToString();
        IsAdmin = user.Role == "Admin";
        RoleText = IsAdmin ? "Quản trị viên" : "Thành viên";
        Initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();

        LoadAvatar(user.UserId);

        Fill(HistoryPreview, _library.GetHistory());
        Fill(FavoritePreview, _library.GetFavorites());
        HasHistory = HistoryPreview.Count > 0;
        HasFavorites = FavoritePreview.Count > 0;
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

    [RelayCommand]
    private async Task OpenStoryAsync(LibraryEntry? entry)
    {
        if (entry is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object>
        {
            { "id", entry.StoryId }
        });
    }

    [RelayCommand]
    private async Task GoToLibraryAsync() => await Shell.Current.GoToAsync("//LibraryPage");

    // Web: nút "Vào trang quản trị" (chỉ hiện với Admin)
    [RelayCommand]
    private async Task GoToAdminAsync() => await Shell.Current.GoToAsync("//AdminStoriesPage");

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        IsAdmin = false;
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

    private static void Fill(ObservableCollection<LibraryEntry> target, IReadOnlyList<LibraryEntry> source)
    {
        target.Clear();
        foreach (var entry in source.Take(PreviewCount)) target.Add(entry);
    }
}

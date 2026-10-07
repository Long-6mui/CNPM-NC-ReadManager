using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "Tài khoản" — gom Đăng nhập / Đăng ký / Đăng xuất như phần góc phải của web.
public partial class AccountViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    private bool isLoggedIn;

    [ObservableProperty] private string displayName = "Thành viên";
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string roleText = string.Empty;
    [ObservableProperty] private string initial = "?";

    public bool IsGuest => !IsLoggedIn;

    public AccountViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    // Gọi mỗi lần tab hiện ra để cập nhật trạng thái sau khi đăng nhập/đăng xuất
    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        IsLoggedIn = await _authService.IsLoggedInAsync();

        var user = _authService.CurrentUser;
        var name = string.IsNullOrWhiteSpace(user?.DisplayName) ? "Thành viên" : user!.DisplayName;
        DisplayName = name;
        Email = user?.Email ?? string.Empty;
        RoleText = user?.Role == "Admin" ? "Quản trị viên" : "Thành viên";
        Initial = name.Trim().Length > 0 ? name.Trim()[..1].ToUpperInvariant() : "?";
    });

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await RunSafeAsync(() => _authService.LogoutAsync());
        IsLoggedIn = false;
        DisplayName = "Thành viên";
        Email = string.Empty;
        RoleText = string.Empty;
        Initial = "?";
    }
}

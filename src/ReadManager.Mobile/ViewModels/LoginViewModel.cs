using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models.Auth;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string usernameOrEmail = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(UsernameOrEmail) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
            return;
        }

        await RunSafeAsync(async () =>
        {
            await _authService.LoginAsync(new LoginRequest
            {
                UsernameOrEmail = UsernameOrEmail.Trim(),
                Password = Password
            });

            // Đăng nhập xong -> vào trang chủ, xoá lịch sử điều hướng để không back lại màn Login
            await Shell.Current.GoToAsync("//StoryListPage");
        });
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.RegisterPage));
    }

    // CHỈ DÙNG ĐỂ DEMO/XEM GIAO DIỆN — bỏ qua đăng nhập thật, nhảy thẳng trang chủ.
    // Xoá command này (và nút tương ứng ở LoginPage.xaml) trước khi nộp bài / lên production.
    [RelayCommand]
    private async Task SkipLoginAsync()
    {
        await Shell.Current.GoToAsync("StoryListPage");
    }
}

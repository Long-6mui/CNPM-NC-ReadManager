using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models.Auth;
using ReadManager.Mobile.Services;
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
            try
            {
                await _authService.LoginAsync(new LoginRequest
                {
                    UsernameOrEmail = UsernameOrEmail.Trim(),
                    Password = Password
                });
            }
            catch (ApiException ex) when (ex.StatusCode == 403)
            {
                // Tài khoản bị khóa / tạm ngừng: hiện đúng lời của API, nếu API không kèm lời thì dùng câu mặc định
                throw new ApiException(403, ex.Message.Contains("HTTP 403")
                    ? "Tài khoản của bạn đang bị khóa hoặc tạm ngừng. Vui lòng liên hệ quản trị viên."
                    : ex.Message, ex.Error);
            }

            SessionFlags.AccountRestricted = false;

            // "//" thay toàn bộ stack để không back lại màn Login.
            await Shell.Current.GoToAsync("//StoryListPage");
        });
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.RegisterPage));
    }

    // Quay về Trang chủ (đăng nhập là tuỳ chọn, giống web)
    [RelayCommand]
    private async Task GoHomeAsync()
    {
        await Shell.Current.GoToAsync("//StoryListPage");
    }
}

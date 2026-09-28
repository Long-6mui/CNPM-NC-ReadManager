using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models.Auth;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ thông tin.";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Mật khẩu nhập lại không khớp.";
            return;
        }

        await RunSafeAsync(async () =>
        {
            await _authService.RegisterAsync(new RegisterRequest
            {
                Username = Username.Trim(),
                Email = Email.Trim(),
                DisplayName = DisplayName.Trim(),
                Password = Password
            });

            // Đăng ký xong -> tự đăng nhập luôn (AuthService đã lưu token) -> vào trang chủ
            await Shell.Current.GoToAsync("//StoryListPage");
        });
    }

    [RelayCommand]
    private async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}

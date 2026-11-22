using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models.Auth;
using ReadManager.Mobile.Services;
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
        if (string.IsNullOrWhiteSpace(Email) ||
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
                Username = string.IsNullOrWhiteSpace(Username) ? Email.Trim().Split('@')[0] : Username.Trim(),
                Email = Email.Trim(),
                DisplayName = DisplayName.Trim(),
                Password = Password
            });

            // Đăng ký xong -> tự đăng nhập luôn rồi mở màn chọn sở thích đọc.
            // Chọn xong, mục "Đề xuất cho bạn" ở trang chủ hiện ngay truyện theo sở thích.
            var loginName = Email.Trim();
            var password = Password;
            Password = string.Empty;
            ConfirmPassword = string.Empty;

            try
            {
                await _authService.LoginAsync(new LoginRequest { UsernameOrEmail = loginName, Password = password });
            }
            catch (ApiException)
            {
                // Máy chủ chưa cho đăng nhập ngay (vd cần xác nhận email) -> về màn đăng nhập như cũ
                await Shell.Current.DisplayAlert("Đăng ký thành công", "Bạn có thể đăng nhập bằng tài khoản vừa tạo.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            SessionFlags.AccountRestricted = false;
            await Shell.Current.GoToAsync("//StoryListPage");
            await Shell.Current.GoToAsync(nameof(Views.ReaderGenresPage));
        });
    }

    [RelayCommand]
    private async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }
}

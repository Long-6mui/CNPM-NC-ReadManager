using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Nền chung cho các màn hình quản trị: kiểm tra quyền Admin, thông báo thành công, nút "Về trang chủ".
public abstract partial class AdminViewModelBase : BaseViewModel
{
    protected readonly IAuthService Auth;

    [ObservableProperty]
    private string? toastMessage;

    protected AdminViewModelBase(IAuthService auth)
    {
        Auth = auth;
    }

    // API vẫn tự chặn người không phải Admin; đây chỉ để đưa họ về trang chủ cho gọn.
    protected async Task<bool> EnsureAdminAsync()
    {
        try { await Auth.IsLoggedInAsync(); }
        catch { /* mất mạng: dùng thông tin hiện có */ }

        if (Auth.CurrentUser?.Role == "Admin") return true;

        await Shell.Current.DisplayAlert("Quản trị", "Bạn cần đăng nhập bằng tài khoản quản trị.", "Đóng");
        await Shell.Current.GoToAsync("//StoryListPage");
        return false;
    }

    [RelayCommand]
    private async Task GoHomeAsync() => await Shell.Current.GoToAsync("//StoryListPage");

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}

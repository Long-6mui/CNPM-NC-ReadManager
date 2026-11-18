using CommunityToolkit.Mvvm.ComponentModel;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    protected async Task RunSafeAsync(Func<Task> action)
    {
        if (IsBusy) return; // chặn bấm nhiều lần khi đang tải
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await action();
        }
        catch (ApiException ex)
        {
            // 401 không kèm lời của server = phiên đăng nhập hết hạn
            ErrorMessage = ex.StatusCode == 401 && ex.Message.Contains("HTTP 401")
                ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
                : ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Hiện địa chỉ đang dùng để dễ tìm nguyên nhân khi demo (sai IP, API chưa chạy, khác Wi-Fi...)
            ErrorMessage = $"Không kết nối được máy chủ ({ApiSettings.BaseUrl}). Kiểm tra API đã chạy chưa và địa chỉ máy chủ trong mục “Địa chỉ máy chủ”.";
        }
        catch (Exception)
        {
            ErrorMessage = "Có lỗi xảy ra. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

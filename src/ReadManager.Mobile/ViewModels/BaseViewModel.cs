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
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể kết nối máy chủ. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

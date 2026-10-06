using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    // ObservableProperty sẽ tự động báo cho giao diện biết khi có dữ liệu mới
    [ObservableProperty]
    private HomeData? _data;

    [ObservableProperty]
    private bool _isBusy;

    public HomeViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    // RelayCommand để gọi hàm này từ nút bấm hoặc khi load trang
    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        // Gọi API lấy dữ liệu trang chủ
        Data = await _apiService.GetHomeDataAsync();

        IsBusy = false;
    }
}
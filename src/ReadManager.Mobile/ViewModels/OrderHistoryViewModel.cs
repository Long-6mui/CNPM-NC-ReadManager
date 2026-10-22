using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Một nút lọc trạng thái ở đầu màn Lịch sử thanh toán
public partial class StatusFilter : ObservableObject
{
    public string Key { get; init; } = string.Empty;     // "" = tất cả
    public string Label { get; init; } = string.Empty;

    [ObservableProperty] private bool isSelected;
}

// PB26 — Lịch sử thanh toán của tôi (GET api/me/orders)
public partial class OrderHistoryViewModel : BaseViewModel
{
    private const int PageSize = 20;

    private readonly IOrderService _orders;
    private readonly IAuthService _auth;
    private int _page = 1;
    private string _status = string.Empty;

    public ObservableCollection<Order> Items { get; } = new();

    public ObservableCollection<StatusFilter> Filters { get; } = new()
    {
        new StatusFilter { Key = "", Label = "Tất cả", IsSelected = true },
        new StatusFilter { Key = "Pending", Label = "Chờ thanh toán" },
        new StatusFilter { Key = "Paid", Label = "Đã thanh toán" },
        new StatusFilter { Key = "Failed", Label = "Thất bại" },
        new StatusFilter { Key = "Expired", Label = "Hết hạn" },
    };

    [ObservableProperty] private bool isLoggedIn = true;
    [ObservableProperty] private bool hasMore;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private string countText = string.Empty;

    public OrderHistoryViewModel(IOrderService orders, IAuthService auth)
    {
        _orders = orders;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try { IsLoggedIn = await _auth.IsLoggedInAsync(); }
        catch { IsLoggedIn = _auth.CurrentUser is not null; }

        if (!IsLoggedIn)
        {
            Items.Clear();
            ShowEmpty = false;
            HasMore = false;
            CountText = string.Empty;
            return;
        }

        _page = 1;
        await RunSafeAsync(async () =>
        {
            var result = await _orders.GetMyOrdersAsync(_status, _page, PageSize);
            Items.Clear();
            foreach (var o in result.Items) Items.Add(o);
            HasMore = _page < result.TotalPages;
            ShowEmpty = Items.Count == 0;
            CountText = result.TotalItems > 0 ? $"{result.TotalItems} đơn hàng" : string.Empty;
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore) return;
        await RunSafeAsync(async () =>
        {
            var next = _page + 1;
            var result = await _orders.GetMyOrdersAsync(_status, next, PageSize);
            foreach (var o in result.Items) Items.Add(o);
            _page = next;
            HasMore = _page < result.TotalPages;
        });
    }

    [RelayCommand]
    private async Task SelectStatusAsync(string? key)
    {
        _status = key ?? string.Empty;
        foreach (var f in Filters) f.IsSelected = f.Key == _status;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenAsync(Order? order)
    {
        if (order is null) return;
        await Shell.Current.GoToAsync(nameof(Views.OrderDetailPage), new Dictionary<string, object>
        {
            { "orderId", order.OrderId }
        });
    }

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");
}

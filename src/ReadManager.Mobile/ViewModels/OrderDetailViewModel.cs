using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// Chi tiết 1 đơn hàng (theo Views/Orders/Details.cshtml của web)
[QueryProperty(nameof(OrderId), "orderId")]
public partial class OrderDetailViewModel : BaseViewModel
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payment;

    [ObservableProperty] private int orderId;
    [ObservableProperty] private Order? order;
    [ObservableProperty] private string infoMessage = string.Empty;

    public OrderDetailViewModel(IOrderService orders, IPaymentService payment)
    {
        _orders = orders;
        _payment = payment;
    }

    partial void OnOrderIdChanged(int value)
    {
        if (value > 0) _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (OrderId <= 0) return;
        await RunSafeAsync(async () =>
        {
            Order = await _orders.GetOrderAsync(OrderId);
            InfoMessage = Order.IsPending
                ? "Đơn hàng đã được tạo và đang chờ thanh toán."
                : string.Empty;
        });
    }

    // "Thanh toán": chờ API của Backend 4 (xem IPaymentService). IsAvailable = true thì gọi API thật.
    [RelayCommand]
    private async Task PayAsync()
    {
        if (Order is null || !Order.ShowPay) return;

        if (!_payment.IsAvailable)
        {
            await Shell.Current.DisplayAlert("Thanh toán", _payment.UnavailableMessage, "Đã hiểu");
            return;
        }

        await RunSafeAsync(async () =>
        {
            var start = await _payment.StartAsync(Order.OrderId);

            InfoMessage = $"Đã tạo giao dịch #{start.PaymentId}, số tiền {start.Amount:N0} {start.Currency}. Đơn chưa được thanh toán.";
#if DEBUG
            // API cũng kiểm tra môi trường Development; bản Release không có nút mô phỏng.
            var choice = await Shell.Current.DisplayActionSheet("Thanh toán thử — không thu tiền thật", "Để sau", null,
                "Thử thành công", "Thử thất bại", "Thử hủy");
            var status = choice switch { "Thử thành công" => "Success", "Thử thất bại" => "Failed", "Thử hủy" => "Cancelled", _ => null };
            if (status is not null)
            {
                var outcome = await _payment.SimulateAsync(Order.OrderId, status);
                InfoMessage = outcome.EntitlementGranted
                    ? "Thanh toán thành công — quyền đọc đã được mở."
                    : outcome.Result == "PaidGrantFailed"
                        ? "Đã thu tiền nhưng chưa mở được quyền đọc. Vui lòng liên hệ quản trị viên."
                        : outcome.Message;
            }
#else
            InfoMessage += " Cổng thanh toán trực tuyến chưa được tích hợp.";
#endif
            Order = await _orders.GetOrderAsync(Order.OrderId);
        });
    }

    [RelayCommand]
    private async Task OpenStoryAsync()
    {
        if (Order?.StoryId is not int id) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object> { { "id", id } });
    }

    [RelayCommand]
    private async Task OpenHistoryAsync() => await Shell.Current.GoToAsync(nameof(Views.OrderHistoryPage));
}

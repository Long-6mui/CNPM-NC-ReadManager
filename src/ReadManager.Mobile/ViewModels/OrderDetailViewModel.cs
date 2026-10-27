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

    // "Thanh toán": gọi API của Backend 4 (xem IPaymentService).
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

            // Cổng thanh toán dạng trang web: mở trình duyệt, trả tiền xong quay lại app (trang tự tải lại)
            if (start.HasPayUrl)
            {
                await Browser.Default.OpenAsync(new Uri(start.PayUrl!), BrowserLaunchMode.SystemPreferred);
                InfoMessage = "Sau khi thanh toán xong, hãy quay lại ứng dụng. Trạng thái đơn sẽ tự cập nhật.";
                return;
            }

            // Chưa có cổng thật: khi API chạy Development cho "thanh toán thử" bằng cổng giả lập của server
            if (_payment.CanSimulate)
            {
                var pick = await Shell.Current.DisplayActionSheet(
                    "Thanh toán thử (cổng giả lập)", "Hủy", null,
                    "✔ Thanh toán thành công", "✖ Thanh toán thất bại", "⏹ Hủy thanh toán");

                var status = pick switch
                {
                    "✔ Thanh toán thành công" => "Success",
                    "✖ Thanh toán thất bại" => "Failed",
                    "⏹ Hủy thanh toán" => "Cancelled",
                    _ => null
                };

                if (status is not null)
                {
                    var result = await _payment.SimulateAsync(Order.OrderId, status);
                    Order = await _orders.GetOrderAsync(Order.OrderId);   // server quyết định kết quả; app chỉ tải lại để hiển thị
                    InfoMessage = result.EntitlementGranted
                        ? "Thanh toán thành công — quyền đọc đã được mở."
                        : result.Result == "PaidGrantFailed"
                            ? "Đã thu tiền nhưng chưa mở được quyền đọc. Quản trị viên sẽ xử lý, vui lòng liên hệ hỗ trợ."
                            : (string.IsNullOrWhiteSpace(result.Message) ? $"Kết quả: {result.Result}" : result.Message);
                    return;
                }
            }

            // Server quyết định kết quả; app chỉ tải lại đơn để hiển thị
            Order = await _orders.GetOrderAsync(Order.OrderId);
            if (!string.IsNullOrWhiteSpace(start.Message)) InfoMessage = start.Message!;
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

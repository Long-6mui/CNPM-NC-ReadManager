using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class CheckoutPage : ContentPage
{
    private readonly CheckoutViewModel _viewModel;

    public CheckoutPage(CheckoutViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Quay lại từ màn Đơn hàng -> cập nhật lại báo giá (vd: đã mua xong / đang có đơn chờ)
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}

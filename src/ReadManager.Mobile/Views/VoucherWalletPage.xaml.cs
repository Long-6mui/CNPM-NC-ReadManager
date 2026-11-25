using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class VoucherWalletPage : ContentPage
{
    private readonly VoucherWalletViewModel _viewModel;

    public VoucherWalletPage(VoucherWalletViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Tải lại mỗi lần mở (vừa đổi voucher / vừa dùng mã khi thanh toán)
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}

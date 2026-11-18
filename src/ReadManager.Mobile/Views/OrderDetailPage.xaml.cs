using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class OrderDetailPage : ContentPage
{
    private readonly OrderDetailViewModel _viewModel;

    public OrderDetailPage(OrderDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Quay lại từ trình duyệt thanh toán -> tải lại trạng thái đơn
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.OrderId > 0) _viewModel.LoadCommand.Execute(null);
    }
}

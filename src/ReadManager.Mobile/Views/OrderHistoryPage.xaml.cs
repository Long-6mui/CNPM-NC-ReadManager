using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class OrderHistoryPage : ContentPage
{
    private readonly OrderHistoryViewModel _viewModel;

    public OrderHistoryPage(OrderHistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}

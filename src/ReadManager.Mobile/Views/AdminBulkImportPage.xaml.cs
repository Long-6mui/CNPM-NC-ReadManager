using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class AdminBulkImportPage : ContentPage
{
    private readonly AdminBulkImportViewModel _viewModel;

    public AdminBulkImportPage(AdminBulkImportViewModel viewModel)
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

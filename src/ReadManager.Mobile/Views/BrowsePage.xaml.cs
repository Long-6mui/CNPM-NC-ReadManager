using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class BrowsePage : ContentPage
{
    private readonly BrowseViewModel _viewModel;

    public BrowsePage(BrowseViewModel viewModel)
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

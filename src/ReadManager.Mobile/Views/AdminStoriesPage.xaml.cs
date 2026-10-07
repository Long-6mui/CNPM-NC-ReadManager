using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class AdminStoriesPage : ContentPage
{
    private readonly AdminStoriesViewModel _viewModel;

    public AdminStoriesPage(AdminStoriesViewModel viewModel)
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

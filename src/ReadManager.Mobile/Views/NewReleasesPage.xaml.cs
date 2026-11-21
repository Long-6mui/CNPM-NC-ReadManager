using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class NewReleasesPage : ContentPage
{
    private readonly NewReleasesViewModel _viewModel;

    public NewReleasesPage(NewReleasesViewModel viewModel)
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

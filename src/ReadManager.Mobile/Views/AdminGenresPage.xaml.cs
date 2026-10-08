using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class AdminGenresPage : ContentPage
{
    private readonly AdminGenresViewModel _viewModel;

    public AdminGenresPage(AdminGenresViewModel viewModel)
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

using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class ReaderGenresPage : ContentPage
{
    private readonly ReaderGenresViewModel _viewModel;

    public ReaderGenresPage(ReaderGenresViewModel viewModel)
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

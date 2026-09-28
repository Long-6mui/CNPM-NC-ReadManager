using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class StoryListPage : ContentPage
{
    private readonly StoryListViewModel _viewModel;

    public StoryListPage(StoryListViewModel viewModel)
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

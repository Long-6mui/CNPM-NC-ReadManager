using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class FollowedStoriesPage : ContentPage
{
    private readonly FollowedStoriesViewModel _viewModel;

    public FollowedStoriesPage(FollowedStoriesViewModel viewModel)
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

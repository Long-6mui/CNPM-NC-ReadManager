using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class CommentsPage : ContentPage
{
    private readonly CommentsViewModel _viewModel;

    public CommentsPage(CommentsViewModel viewModel)
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

using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class AdminChapterEditPage : ContentPage
{
    private readonly AdminChapterEditViewModel _viewModel;

    public AdminChapterEditPage(AdminChapterEditViewModel viewModel)
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

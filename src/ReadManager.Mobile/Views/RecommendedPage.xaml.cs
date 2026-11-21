using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class RecommendedPage : ContentPage
{
    private readonly RecommendedViewModel _viewModel;

    public RecommendedPage(RecommendedViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Tải lại mỗi lần hiện ra (có thể vừa sửa sở thích)
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}

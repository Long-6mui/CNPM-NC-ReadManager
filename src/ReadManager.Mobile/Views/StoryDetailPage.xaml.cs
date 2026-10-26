using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class StoryDetailPage : ContentPage
{
    private readonly StoryDetailViewModel _viewModel;

    public StoryDetailPage(StoryDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Quay lại từ màn mua -> cập nhật nút "Mua / Đã sở hữu"
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshPurchaseAsync();
        await _viewModel.RefreshFollowAsync();
    }
}

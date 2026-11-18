using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class PlansPage : ContentPage
{
    private readonly PlansViewModel _viewModel;

    public PlansPage(PlansViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Tải lại mỗi lần mở: sau khi mua xong quay lại sẽ thấy cập nhật
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}

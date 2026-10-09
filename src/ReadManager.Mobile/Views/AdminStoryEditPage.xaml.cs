using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class AdminStoryEditPage : ContentPage
{
    private readonly AdminStoryEditViewModel _viewModel;
    private bool _firstAppearing = true;

    public AdminStoryEditPage(AdminStoryEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_firstAppearing)
        {
            _firstAppearing = false;
            _viewModel.LoadCommand.Execute(null);          // lần đầu: nạp cả form
        }
        else
        {
            _viewModel.ReloadChaptersCommand.Execute(null); // quay lại từ màn chương: chỉ nạp lại danh sách chương
        }
    }
}

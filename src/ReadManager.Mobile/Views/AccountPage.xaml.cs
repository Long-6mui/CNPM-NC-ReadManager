using ReadManager.Mobile.Behaviors;
using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class AccountPage : ContentPage
{
    private readonly AccountViewModel _viewModel;
    private bool _firstAppear = true;

    public AccountPage(AccountViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Đăng xuất xong -> header ẩn chuỗi 🔥 / số thông báo
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountViewModel.IsLoggedIn)) _ = Header.RefreshAsync();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = Header.RefreshAsync();
        if (_firstAppear)
        {
            _firstAppear = false;
            _ = Motion.PageEnterAsync(Body);
        }
        _viewModel.LoadCommand.Execute(null);
    }
}

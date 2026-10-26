using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class ChapterReaderPage : ContentPage
{
    private readonly ChapterReaderViewModel _viewModel;

    public ChapterReaderPage(ChapterReaderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Quay lại từ màn mua: nếu chương vừa được mở khóa thì hiện nội dung
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.ReloadIfLockedAsync();
    }
}

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

    // Nhớ vị trí cuộn (%) để lưu cùng điểm "Đọc tiếp"
    private void OnReaderScrolled(object? sender, ScrolledEventArgs e)
    {
        var scrollable = ReaderScroll.ContentSize.Height - ReaderScroll.Height;
        if (scrollable > 0)
            _viewModel.ScrollPercent = (decimal)Math.Clamp(e.ScrollY / scrollable * 100.0, 0, 100);
    }

    // Rời màn đọc (qua chương khác, quay lại...) -> lưu điểm đọc tiếp kèm vị trí cuộn
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.SaveProgressNowAsync();
    }
}

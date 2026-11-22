using ReadManager.Mobile.Services;
using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class SearchPage : ContentPage
{
    private readonly SearchViewModel _viewModel;
    private bool _focusedOnce;

    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StatusBarHelper.UseDefault();
        SearchGlyph.Source = ThemeService.IsDark ? "ic_search_white.png" : "ic_search_ink.png";
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Mở trang là bật bàn phím ngay (giống ảnh mẫu)
        if (!_focusedOnce)
        {
            _focusedOnce = true;
            await Task.Delay(250);
            QueryEntry.Focus();
        }
    }

    // Viền ô tìm kiếm sáng lên khi đang gõ (như :focus của web)
    private void OnQueryFocused(object? sender, FocusEventArgs e)
    {
        SearchBox.Stroke = ThemeService.Get("WebRoyal");
        _ = PulseAsync();
    }

    private async Task PulseAsync()
    {
        await SearchBox.ScaleTo(1.02, 140, Easing.CubicOut);
        await SearchBox.ScaleTo(1, 180, Easing.CubicOut);
    }

    private void OnQueryUnfocused(object? sender, FocusEventArgs e)
        => SearchBox.Stroke = ThemeService.Get("WebBorder");
}

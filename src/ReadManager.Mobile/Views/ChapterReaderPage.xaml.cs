using ReadManager.Mobile.ViewModels;
namespace ReadManager.Mobile.Views;

public partial class ChapterReaderPage : ContentPage
{
    private readonly ChapterReaderViewModel _viewModel;
    private CancellationTokenSource? _debounce;
    private bool _restored;
    private bool _restoring;
    private bool _active;
    public ChapterReaderPage(ChapterReaderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.Chapter)) _restored = false;
            if (e.PropertyName == nameof(viewModel.ProgressReady)) Dispatcher.Dispatch(TryRestore);
        };
        ReaderScroll.Content.SizeChanged += (_, _) => TryRestore();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _active = true;
        await _viewModel.ReloadIfLockedAsync();
        TryRestore();
    }
    private void OnReaderSizeChanged(object? sender, EventArgs e) => TryRestore();
    private async void TryRestore()
    {
        if (!_active || _restored || _restoring || !_viewModel.ProgressReady || ReaderScroll.Height <= 0 || ReaderScroll.Content.Height <= 0) return;
        _restoring = true;
        try
        {
            var range = Math.Max(0, ReaderScroll.ContentSize.Height - ReaderScroll.Height);
            await ReaderScroll.ScrollToAsync(0, range * (double)_viewModel.ScrollPercent / 100, false);
            _restored = true;
            await _viewModel.SaveScrollAsync(_viewModel.ScrollPercent);
        }
        finally { _restoring = false; }
    }
    private decimal Position()
    {
        var range = Math.Max(0, ReaderScroll.ContentSize.Height - ReaderScroll.Height);
        return range > 0 ? (decimal)Math.Clamp(ReaderScroll.ScrollY / range * 100, 0, 100) : 0;
    }
    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (!_active || !_restored || _restoring) return;
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(700, cts.Token);
            await _viewModel.SaveScrollAsync(Position());
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_debounce, cts)) _debounce = null; cts.Dispose(); }
    }
    protected override async void OnDisappearing()
    {
        _active = false;
        _debounce?.Cancel();
        base.OnDisappearing();
        if (_restored) await _viewModel.SaveScrollAsync(Position());
    }
}

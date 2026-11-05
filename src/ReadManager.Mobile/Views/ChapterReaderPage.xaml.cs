using ReadManager.Mobile.ViewModels;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;
namespace ReadManager.Mobile.Views;

public partial class ChapterReaderPage : ContentPage
{
    private readonly ChapterReaderViewModel _viewModel;
    private CancellationTokenSource? _debounce;
    private bool _restored;
    private bool _restoring;
    private bool _active;
    private readonly ReaderActivityService _reader;
    private readonly IAuthService _auth;
    private CancellationTokenSource? _reward;
    public ChapterReaderPage(ChapterReaderViewModel viewModel, ReaderActivityService reader, IAuthService auth)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _reader = reader;
        _auth = auth;
        BindingContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.Chapter)) { _restored = false; _reward?.Cancel(); }
            if (e.PropertyName == nameof(viewModel.ProgressReady)) Dispatcher.Dispatch(TryRestore);
        };
        ReaderScroll.Content.SizeChanged += (_, _) => TryRestore();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _active = true;
        if (Window is { } window) { window.Stopped += OnStopped; window.Resumed += OnResumed; }
        if (_restored) BeginReward();
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
            BeginReward();
            await _viewModel.SaveScrollAsync(_viewModel.ScrollPercent);
        }
        finally { _restoring = false; }
    }
    private void OnStopped(object? sender, EventArgs e) => _reward?.Cancel();
    private void OnResumed(object? sender, EventArgs e) { if (_restored) BeginReward(); }

    private void BeginReward()
    {
        _reward?.Cancel();
        if (!_active || !_viewModel.IsReadable || _auth.CurrentUser is null) return;
        var session = _reward = new CancellationTokenSource();
        _ = TrackRewardAsync(_viewModel.Chapter!.ChapterId, session);
    }

    private async Task TrackRewardAsync(int chapterId, CancellationTokenSource session)
    {
        try
        {
            var ticket = await _reader.Start(chapterId);
            if (ticket is null) return;
            await Task.Delay(TimeSpan.FromSeconds(60), session.Token);
            while (ReaderScroll.ContentSize.Height > ReaderScroll.Height + 1 && Position() < 99)
                await Task.Delay(500, session.Token);
            session.Token.ThrowIfCancellationRequested();
            var percent = ReaderScroll.ContentSize.Height <= ReaderScroll.Height + 1 ? 100 : Position();
            var outcome = await _reader.Finish(chapterId, ticket.Ticket, percent);
            if (outcome?.Granted == true && !session.IsCancellationRequested)
                _viewModel.ProgressMessage = "+10 EXP";
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        catch (Exception ex) { if (_active && !session.IsCancellationRequested) _viewModel.ProgressMessage = $"Chưa ghi nhận EXP: {ex.Message}"; }
        finally { if (ReferenceEquals(_reward, session)) _reward = null; session.Dispose(); }
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
        _reward?.Cancel();
        if (Window is { } window) { window.Stopped -= OnStopped; window.Resumed -= OnResumed; }
        _debounce?.Cancel();
        base.OnDisappearing();
        if (_restored) await _viewModel.SaveScrollAsync(Position());
    }
}

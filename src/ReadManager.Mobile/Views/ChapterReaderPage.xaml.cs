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
            if (e.PropertyName == nameof(viewModel.Chapter))
            {
                _restored = false;
                _reward?.Cancel();
                _ticket = null;   // chương mới -> lấy vé mới
                Dispatcher.Dispatch(() => RewardPill.IsVisible = false);
            }
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

    // ===================== THƯỞNG ĐỌC CHƯƠNG (xu + chuỗi 🔥) =====================
    // Quy tắc của API (reader/chapters/{id}/start → finish): phải mở chương ít nhất 60 giây VÀ đọc tới cuối.
    // Bản cũ chỉ gửi "finish" khi người đọc vẫn ĐỨNG Ở chương đó đủ 60 giây → đọc nhanh rồi bấm "Chương sau"
    // hoặc thoát ra là mất thưởng, chuỗi không tăng.
    // Bản mới: lấy vé khi mở chương; khi đã cuộn tới cuối thì hẹn gửi "finish" đúng lúc đủ 60 giây,
    // việc hẹn này KHÔNG bị hủy khi rời trang → đọc xong là chắc chắn được ghi nhận.
    private static readonly HashSet<int> Finishing = new();   // chương đang chờ / đã gửi finish trong lần chạy app này
    private int _ticketChapter;
    private string? _ticket;
    private DateTime _ticketAt;
    private bool _reachedEnd;

    private void BeginReward()
    {
        _reward?.Cancel();
        if (!_active || !_viewModel.IsReadable || _viewModel.Chapter is null) return;
        var session = _reward = new CancellationTokenSource();
        _ = TrackRewardAsync(_viewModel.Chapter.ChapterId, session);
    }

    private async Task TrackRewardAsync(int chapterId, CancellationTokenSource session)
    {
        try
        {
            bool loggedIn;
            try { loggedIn = _auth.CurrentUser is not null || await _auth.IsLoggedInAsync(); }
            catch { loggedIn = _auth.CurrentUser is not null; }
            if (!loggedIn) { ShowReward("Đăng nhập để nhận xu và giữ chuỗi đọc 🔥"); return; }

            // Vé mới cho mỗi chương (vé cũ của chương khác không dùng lại)
            if (_ticketChapter != chapterId || _ticket is null)
            {
                _reachedEnd = false;
                var ticket = await _reader.Start(chapterId);
                if (ticket is null || string.IsNullOrEmpty(ticket.Ticket)) return;
                _ticket = ticket.Ticket;
                _ticketAt = DateTime.UtcNow;
                _ticketChapter = chapterId;
            }

            if (Finishing.Contains(chapterId)) return;
            ShowReward("🎁 Đọc hết chương để nhận +10 xu và giữ chuỗi 🔥");
            CheckReachedEnd();

            // Chờ tới khi cuộn tới cuối (OnScrolled cũng kiểm tra) — vẫn chờ khi trang đang mở
            while (!_reachedEnd)
            {
                await Task.Delay(500, session.Token);
                CheckReachedEnd();
            }
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (_active && !session.IsCancellationRequested) ShowReward($"Chưa ghi nhận xu: {ex.Message}");
        }
        finally { if (ReferenceEquals(_reward, session)) _reward = null; session.Dispose(); }
    }

    // Đã cuộn tới cuối chương (hoặc chương ngắn vừa một màn hình) → hẹn gửi "finish"
    private void CheckReachedEnd()
    {
        if (_reachedEnd || _ticket is null || _viewModel.Chapter?.ChapterId != _ticketChapter) return;
        var fits = ReaderScroll.ContentSize.Height <= ReaderScroll.Height + 1;
        if (!fits && Position() < 97) return;   // 97%: chừa sai số làm tròn khi cuộn chạm đáy trên Android
        _reachedEnd = true;
        _ = FinishWhenReadyAsync(_ticketChapter, _ticket, _ticketAt);
    }

    private async Task FinishWhenReadyAsync(int chapterId, string ticket, DateTime ticketAt)
    {
        if (!Finishing.Add(chapterId)) return;
        try
        {
            // Đủ 60 giây kể từ lúc mở chương (thêm 2 giây cho chắc), có đếm ngược trên màn hình
            var readyAt = ticketAt.AddSeconds(62);
            while (DateTime.UtcNow < readyAt)
            {
                var left = (int)Math.Ceiling((readyAt - DateTime.UtcNow).TotalSeconds);
                if (_viewModel.Chapter?.ChapterId == chapterId)
                    ShowReward($"⏳ Đọc thêm {left} giây nữa để nhận +10 xu 🔥");
                await Task.Delay(1000);
            }

            var outcome = await _reader.Finish(chapterId, ticket, 100);
            RewardsService.NotifyCoinsChanged();
            if (outcome is null) return;
            var text = outcome.CoinsAdded > 0
                ? $"🎉 +{outcome.CoinsAdded} xu · Chuỗi {outcome.CurrentStreak} ngày 🔥"
                : $"✅ Đã ghi nhận đọc hôm nay · Chuỗi {outcome.CurrentStreak} ngày 🔥";
            if (_viewModel.Chapter?.ChapterId == chapterId) ShowReward(text, celebrate: true);
            else _viewModel.ProgressMessage = text;
        }
        catch (Exception ex)
        {
            Finishing.Remove(chapterId);   // lỗi mạng: mở lại chương để thử lại
            if (_viewModel.Chapter?.ChapterId == chapterId) ShowReward($"Chưa ghi nhận xu: {ex.Message}");
        }
    }

    private void ShowReward(string text, bool celebrate = false) => Dispatcher.Dispatch(async () =>
    {
        RewardLabel.Text = text;
        if (!RewardPill.IsVisible)
        {
            RewardPill.Opacity = 0;
            RewardPill.TranslationY = 20;
            RewardPill.IsVisible = true;
            await Task.WhenAll(RewardPill.FadeTo(1, 250), RewardPill.TranslateTo(0, 0, 300, Easing.CubicOut));
        }
        if (celebrate)
        {
            await RewardPill.ScaleTo(1.12, 160, Easing.CubicOut);
            await RewardPill.ScaleTo(1, 300, Easing.SpringOut);
            await Task.Delay(4000);
            if (RewardLabel.Text == text) await RewardPill.FadeTo(0, 400);
            if (RewardLabel.Text == text) RewardPill.IsVisible = false;
        }
    });

    private decimal Position()
    {
        var range = Math.Max(0, ReaderScroll.ContentSize.Height - ReaderScroll.Height);
        return range > 0 ? (decimal)Math.Clamp(ReaderScroll.ScrollY / range * 100, 0, 100) : 0;
    }
    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (!_active || !_restored || _restoring) return;
        CheckReachedEnd();
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

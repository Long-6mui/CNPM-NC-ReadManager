using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

[QueryProperty(nameof(ChapterId), "chapterId")]
public partial class ChapterReaderViewModel : BaseViewModel
{
    private readonly IChapterService _chapterService;
    private readonly IReadingProgressService _progress;
    private readonly ReportFlow _report;

    [ObservableProperty]
    private int chapterId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    [NotifyPropertyChangedFor(nameof(IsReadable))]
    private ChapterContent? chapter;

    // Chương trả phí chưa mở khóa (API trả IsLocked = true, nội dung để trống)
    public bool IsLocked => Chapter?.IsLocked == true;
    public bool IsReadable => Chapter is not null && !Chapter.IsLocked;

    public ChapterReaderViewModel(IChapterService chapterService, IReadingProgressService progress, ReportFlow report)
    {
        _chapterService = chapterService;
        _progress = progress;
        _report = report;
    }

    partial void OnChapterIdChanged(int value)
    {
        _ = RunSafeAsync(async () =>
        {
            ProgressReady = false;
            Chapter = await _chapterService.GetChapterContentAsync(value);
            if (!Chapter.IsLocked) await RestoreProgressAsync(); // "Đang đọc / Đọc tiếp": nhớ chương đang đọc
        });
    }

    // Gọi khi quay lại từ màn mua: nếu vừa mở khóa thì tải lại nội dung
    public Task ReloadIfLockedAsync() => IsLocked && ChapterId > 0 ? ReloadAsync() : Task.CompletedTask;

    private Task ReloadAsync() => RunSafeAsync(async () =>
    {
        ProgressReady = false;
        Chapter = await _chapterService.GetChapterContentAsync(ChapterId);
        if (!Chapter.IsLocked) await RestoreProgressAsync();
    });

    [ObservableProperty] private decimal scrollPercent;
    [ObservableProperty] private bool progressReady;
    [ObservableProperty] private string progressMessage = string.Empty;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private async Task RestoreProgressAsync()
    {
        ProgressReady = false;
        ScrollPercent = 0;
        try
        {
            var saved = await _progress.GetProgressAsync(Chapter!.StoryId);
            if (saved?.ChapterId == Chapter.ChapterId) ScrollPercent = saved.ScrollPercent;
            ProgressMessage = string.Empty;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            ProgressMessage = "Chưa tải được tiến độ từ máy chủ. Bạn vẫn có thể đọc chương.";
        }
        ProgressReady = true;
    }

    public async Task SaveScrollAsync(decimal percent)
    {
        if (!IsReadable || !ProgressReady) return;
        var current = Chapter!;
        ScrollPercent = Math.Clamp(percent, 0, 100);
        await _saveGate.WaitAsync();
        try
        {
            await _progress.SaveAsync(current.StoryId, current.ChapterId, current.ChapterNumber, current.Title, percent);
            ProgressMessage = string.Empty;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            ProgressMessage = "Đã nhớ trên máy; chưa đồng bộ tiến độ. Kiểm tra kết nối hoặc đăng nhập lại.";
        }
        finally { _saveGate.Release(); }
    }

    // Chương trả phí: mở màn "Mua trọn bộ" (PB21)
    [RelayCommand]
    private async Task BuyStoryAsync()
    {
        if (Chapter is null) return;
        await Shell.Current.GoToAsync(nameof(Views.CheckoutPage), new Dictionary<string, object>
        {
            { "storyId", Chapter.StoryId }
        });
    }

    // Bình luận của riêng chương này (Sprint 3)
    [RelayCommand]
    private async Task OpenCommentsAsync()
    {
        if (Chapter is null) return;
        await Shell.Current.GoToAsync(nameof(Views.CommentsPage), new Dictionary<string, object>
        {
            { "storyId", Chapter.StoryId },
            { "chapterId", Chapter.ChapterId },
            { "heading", $"Bình luận chương {Chapter.ChapterNumber}" }
        });
    }

    [RelayCommand]
    private Task ReportChapterAsync()
        => Chapter is null ? Task.CompletedTask : _report.RunAsync("Chapter", Chapter.ChapterId, "chương");

    [RelayCommand]
    private async Task OpenPlansAsync() => await Shell.Current.GoToAsync(nameof(Views.PlansPage));

    [RelayCommand]
    private async Task GoToPreviousAsync()
    {
        if (Chapter?.PreviousChapterId is int prevId)
            await NavigateToChapterAsync(prevId);
    }

    [RelayCommand]
    private async Task GoToNextAsync()
    {
        if (Chapter?.NextChapterId is int nextId)
            await NavigateToChapterAsync(nextId);
    }

    private async Task NavigateToChapterAsync(int targetChapterId)
    {
        // Điều hướng lại chính trang này với chapterId mới để tải nội dung chương khác
        await Shell.Current.GoToAsync($"{nameof(Views.ChapterReaderPage)}?chapterId={targetChapterId}");
    }
}

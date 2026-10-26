using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Sprint 3 — Bình luận của một truyện, hoặc của một chương nếu có chapterId.
[QueryProperty(nameof(StoryId), "storyId")]
[QueryProperty(nameof(ChapterId), "chapterId")]
[QueryProperty(nameof(Heading), "heading")]
public partial class CommentsViewModel : BaseViewModel
{
    private const int PageSize = 20;
    private const int MaxLength = 1000;

    private readonly ICommunityService _community;
    private readonly IAuthService _auth;
    private readonly ReportFlow _report;
    private int _page = 1;

    public ObservableCollection<CommentItem> Items { get; } = new();

    [ObservableProperty] private int storyId;
    [ObservableProperty] private int chapterId;       // 0 = bình luận của cả truyện
    [ObservableProperty] private string heading = "Bình luận";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LengthText))]
    private string newComment = string.Empty;

    [ObservableProperty] private bool isLoggedIn;
    [ObservableProperty] private bool isSending;
    [ObservableProperty] private bool hasMore;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private string countText = string.Empty;

    public string LengthText => $"{NewComment?.Length ?? 0}/{MaxLength}";

    public CommentsViewModel(ICommunityService community, IAuthService auth, ReportFlow report)
    {
        _community = community;
        _auth = auth;
        _report = report;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (StoryId <= 0) return;

        try { IsLoggedIn = await _auth.IsLoggedInAsync(); }
        catch { IsLoggedIn = _auth.CurrentUser is not null; }

        _page = 1;
        await RunSafeAsync(async () =>
        {
            var result = await _community.GetCommentsAsync(StoryId, ChapterId > 0 ? ChapterId : null, _page, PageSize);
            Items.Clear();
            foreach (var c in result.Items) Items.Add(c);
            HasMore = _page < result.TotalPages;
            ShowEmpty = Items.Count == 0;
            CountText = result.TotalItems > 0 ? $"{result.TotalItems} bình luận" : string.Empty;
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore) return;
        await RunSafeAsync(async () =>
        {
            var next = _page + 1;
            var result = await _community.GetCommentsAsync(StoryId, ChapterId > 0 ? ChapterId : null, next, PageSize);
            foreach (var c in result.Items) Items.Add(c);
            _page = next;
            HasMore = _page < result.TotalPages;
        });
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = NewComment?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            ErrorMessage = "Vui lòng nhập nội dung bình luận.";
            return;
        }
        if (text.Length > MaxLength)
        {
            ErrorMessage = $"Bình luận tối đa {MaxLength} ký tự.";
            return;
        }
        if (IsSending) return;

        try
        {
            IsSending = true;
            ErrorMessage = null;
            var created = await _community.AddCommentAsync(StoryId, ChapterId > 0 ? ChapterId : null, text);
            Items.Insert(0, created);   // bình luận mới lên đầu
            NewComment = string.Empty;
            ShowEmpty = false;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;  // 400 nội dung sai, 403 bị hạn chế, 429 gửi quá nhanh (5 lần/phút)...
        }
        catch
        {
            ErrorMessage = "Không thể kết nối máy chủ. Vui lòng thử lại.";
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CommentItem? comment)
    {
        if (comment is null) return;
        var ok = await Shell.Current.DisplayAlert("Xóa bình luận", "Bạn chắc chắn muốn xóa bình luận này?", "Xóa", "Hủy");
        if (!ok) return;

        await RunSafeAsync(async () =>
        {
            await _community.DeleteCommentAsync(comment.CommentId);
            Items.Remove(comment);
            ShowEmpty = Items.Count == 0;
        });
    }

    [RelayCommand]
    private Task ReportAsync(CommentItem? comment)
        => comment is null ? Task.CompletedTask : _report.RunAsync("Comment", comment.CommentId, "bình luận");

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");
}

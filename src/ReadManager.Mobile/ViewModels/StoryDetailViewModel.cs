using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Chi tiết truyện: API chỉ có GET api/stories/{id} nên điều hướng truyền "id" (không dùng slug).
[QueryProperty(nameof(StoryId), "id")]
public partial class StoryDetailViewModel : BaseViewModel
{
    private readonly IStoryService _storyService;
    private readonly ILibraryService _library;
    private readonly IOrderService _orders;
    private readonly ICommunityService _community;
    private readonly IAuthService _auth;
    private readonly ReportFlow _report;

    [ObservableProperty]
    private int storyId;

    [ObservableProperty]
    private StoryDetail? story;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteText))]
    private bool isFavorite;

    public string FavoriteText => IsFavorite ? "♥ Đã yêu thích" : "♡ Yêu thích";

    // ----- Mua trọn bộ (PB21): lấy từ báo giá của API -----
    [ObservableProperty] private bool showBuy;
    [ObservableProperty] private bool showOwned;
    [ObservableProperty] private string buyText = string.Empty;

    // ----- Theo dõi truyện (Sprint 3) -----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowText))]
    private bool isFollowing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowerText))]
    private int followerCount;

    public string FollowText => IsFollowing ? "🔔 Đang theo dõi" : "＋ Theo dõi";
    public string FollowerText => $"{FollowerCount} người theo dõi";

    public StoryDetailViewModel(IStoryService storyService, ILibraryService library, IOrderService orders,
        ICommunityService community, IAuthService auth, ReportFlow report)
    {
        _community = community;
        _auth = auth;
        _report = report;
        _storyService = storyService;
        _library = library;
        _orders = orders;
    }

    // Shell tự gán StoryId khi điều hướng tới trang này
    partial void OnStoryIdChanged(int value)
    {
        if (value <= 0) return;
        _ = RunSafeAsync(async () =>
        {
            Story = await _storyService.GetStoryByIdAsync(value);

            // Ghi vào "Truyện đã xem gần đây" của Tủ sách
            _library.AddToHistory(Story);
            IsFavorite = _library.IsFavorite(Story.StoryId);
        });
        _ = RefreshPurchaseAsync();
    }

    // Cập nhật nút "Mua trọn bộ / Đã sở hữu". Gọi lại mỗi lần quay về màn này (sau khi mua).
    public async Task RefreshPurchaseAsync()
    {
        ShowBuy = false;
        ShowOwned = false;
        if (StoryId <= 0) return;

        try
        {
            var quote = await _orders.QuoteStoryAsync(StoryId);
            if (quote is null) return;

            ShowOwned = quote.AlreadyOwned;
            ShowBuy = quote.CanBuy;
            BuyText = quote.PendingOrderId.HasValue
                ? $"Tiếp tục thanh toán — {quote.AmountText}"
                : $"🛒 Mua trọn bộ — {quote.AmountText}";
        }
        catch { /* truyện miễn phí / mất mạng: không hiện nút mua */ }
    }

    // Trạng thái theo dõi + số người theo dõi. Gọi lại mỗi lần màn hiện ra.
    public async Task RefreshFollowAsync()
    {
        if (StoryId <= 0) return;
        try
        {
            var status = await _community.GetFollowAsync(StoryId);
            IsFollowing = status.IsFollowing;
            FollowerCount = status.FollowerCount;
        }
        catch { /* mất mạng: giữ trạng thái cũ */ }
    }

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        if (StoryId <= 0) return;

        bool loggedIn;
        try { loggedIn = await _auth.IsLoggedInAsync(); }
        catch { loggedIn = _auth.CurrentUser is not null; }

        if (!loggedIn)
        {
            if (await Shell.Current.DisplayAlert("Cần đăng nhập", "Đăng nhập để theo dõi truyện và nhận thông báo chương mới.", "Đăng nhập", "Để sau"))
                await Shell.Current.GoToAsync("//LoginPage");
            return;
        }

        try
        {
            var status = await _community.SetFollowAsync(StoryId, !IsFollowing);
            IsFollowing = status.IsFollowing;
            FollowerCount = status.FollowerCount;
        }
        catch (ApiException ex)
        {
            await Shell.Current.DisplayAlert("Theo dõi truyện", ex.Message, "Đóng");
        }
        catch
        {
            await Shell.Current.DisplayAlert("Theo dõi truyện", "Không thể kết nối máy chủ. Vui lòng thử lại.", "Đóng");
        }
    }

    [RelayCommand]
    private async Task OpenCommentsAsync()
    {
        if (StoryId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.CommentsPage), new Dictionary<string, object>
        {
            { "storyId", StoryId },
            { "heading", "Bình luận truyện" }
        });
    }

    [RelayCommand]
    private Task ReportStoryAsync() => _report.RunAsync("Story", StoryId, "truyện");

    [RelayCommand]
    private async Task BuyAsync()
    {
        if (StoryId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.CheckoutPage), new Dictionary<string, object>
        {
            { "storyId", StoryId }
        });
    }

    [RelayCommand]
    private async Task OpenChaptersAsync()
    {
        if (StoryId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.ChapterListPage), new Dictionary<string, object>
        { ["storyId"] = StoryId, ["storyTitle"] = Story?.Title ?? "Danh sách chương" });
    }

    // Nút "Yêu thích" -> thêm/bỏ khỏi Tủ sách
    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Story is null) return;
        IsFavorite = _library.ToggleFavorite(Story);
    }
}

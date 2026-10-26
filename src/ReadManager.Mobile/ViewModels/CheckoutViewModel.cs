using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Màn "Mua trọn bộ truyện" (PB21) hoặc "Mua / gia hạn gói" (PB19): xem giá -> bấm Thanh toán để tạo đơn.
// Điều hướng truyền "storyId" (mua truyện) hoặc "planId" (mua gói).
[QueryProperty(nameof(StoryId), "storyId")]
[QueryProperty(nameof(PlanId), "planId")]
public partial class CheckoutViewModel : BaseViewModel
{
    private readonly IOrderService _orders;
    private readonly IStoryService _stories;
    private readonly IAuthService _auth;

    private bool _loaded;
    private OrderQuote? _quote;

    [ObservableProperty] private int storyId;
    [ObservableProperty] private int planId;

    [ObservableProperty] private string heading = "Thanh toán";
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private string amountText = string.Empty;
    [ObservableProperty] private string? coverUrl;
    [ObservableProperty] private bool hasCover;
    [ObservableProperty] private bool hasNoCover = true;
    [ObservableProperty] private bool showContent;
    [ObservableProperty] private bool canBuy;
    [ObservableProperty] private string notice = string.Empty;
    [ObservableProperty] private string cannotBuyMessage = string.Empty;
    [ObservableProperty] private string confirmText = "💳 Thanh toán";

    public ObservableCollection<string> Benefits { get; } = new();

    public CheckoutViewModel(IOrderService orders, IStoryService stories, IAuthService auth)
    {
        _orders = orders;
        _stories = stories;
        _auth = auth;
    }

    // Shell gán StoryId / PlanId khi điều hướng tới trang này
    partial void OnStoryIdChanged(int value) { if (value > 0) _ = LoadAsync(); }
    partial void OnPlanIdChanged(int value) { if (value > 0) _ = LoadAsync(); }

    // Gọi khi quay lại màn này (vd: từ màn Đơn hàng) để cập nhật "đang có đơn chờ"
    public Task RefreshAsync() => _loaded ? LoadAsync() : Task.CompletedTask;

    private async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            var isStory = StoryId > 0;
            _quote = isStory ? await _orders.QuoteStoryAsync(StoryId) : await _orders.QuotePlanAsync(PlanId);
            if (_quote is null)
            {
                ErrorMessage = isStory ? "Không tìm thấy truyện." : "Không tìm thấy gói đọc.";
                ShowContent = false;
                return;
            }

            Heading = isStory ? "🛒 Mua trọn bộ truyện" : "🎟 Mua / gia hạn gói đọc";
            Title = _quote.ItemName;
            AmountText = _quote.AmountText;
            CanBuy = _quote.CanBuy;
            CannotBuyMessage = _quote.CanBuy ? string.Empty : (_quote.Message ?? "Không thể mua nội dung này.");
            Notice = _quote.PendingOrderId.HasValue
                ? "Bạn đang có một đơn chờ thanh toán cho món này — bấm “Tiếp tục thanh toán” để mở lại đơn đó."
                : string.Empty;
            ConfirmText = _quote.PendingOrderId.HasValue
                ? "Tiếp tục thanh toán"
                : $"💳 Thanh toán {_quote.AmountText}";

            Benefits.Clear();
            if (isStory)
            {
                Subtitle = string.Empty;
                try
                {
                    var story = await _stories.GetStoryByIdAsync(StoryId);
                    Subtitle = $"{story.AuthorText} · {story.PublishedChapterCount} chương đã ra";
                    CoverUrl = story.CoverUrl;
                    HasCover = story.HasCover;
                    HasNoCover = !story.HasCover;
                }
                catch { /* không có thông tin phụ vẫn mua được */ }

                Benefits.Add("✔ Đọc vĩnh viễn tất cả chương của truyện, kể cả các chương ra sau này");
                Benefits.Add("✔ Chỉ trả tiền một lần, không có hạn sử dụng");
                Benefits.Add("✔ Quyền đọc gắn với tài khoản, dùng được trên cả Web và Mobile");
            }
            else
            {
                Subtitle = _quote.DurationMonths.HasValue ? $"Thời hạn {_quote.DurationMonths} tháng" : string.Empty;
                HasCover = false;
                HasNoCover = true;
                Benefits.Add("✔ Mở khóa các truyện nằm trong phạm vi của gói");
                Benefits.Add("✔ Gia hạn khi còn hạn sẽ được cộng dồn thời gian");
                Benefits.Add("✔ Giá lấy theo phiên bản gói mới nhất đang bán");
            }

            ShowContent = true;
            _loaded = true;
        });
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (_quote is null || !_quote.CanBuy) return;

        // Phải đăng nhập mới tạo được đơn
        var loggedIn = false;
        try { loggedIn = await _auth.IsLoggedInAsync(); } catch { }
        if (!loggedIn)
        {
            await Shell.Current.DisplayAlert("Cần đăng nhập", "Hãy đăng nhập để mua nội dung này.", "Đăng nhập");
            await Shell.Current.GoToAsync("//LoginPage");
            return;
        }

        await RunSafeAsync(async () =>
        {
            // Đã có đơn chờ -> mở lại đơn đó (server cũng tự dùng lại đơn chờ, bấm 2 lần không tạo 2 đơn)
            var order = StoryId > 0
                ? await _orders.CreateStoryOrderAsync(StoryId)
                : await _orders.CreatePlanOrderAsync(PlanId, _quote.PlanVersionId);

            // "../" = đóng màn Thanh toán rồi mở màn Đơn hàng, nên nút Back từ đơn hàng quay về trang trước đó
            await Shell.Current.GoToAsync($"../{nameof(Views.OrderDetailPage)}", new Dictionary<string, object>
            {
                { "orderId", order.OrderId }
            });
        });
    }
}

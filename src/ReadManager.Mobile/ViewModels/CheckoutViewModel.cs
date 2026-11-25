using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Màn "Mua trọn bộ truyện" (PB21) hoặc "Mua / gia hạn gói" (PB19): xem giá -> chọn mã giảm giá -> Thanh toán.
// Điều hướng truyền "storyId" (mua truyện) hoặc "planId" (mua gói).
// Mã giảm giá:
//  - Mua TRUYỆN: chọn voucher trong kho (đổi bằng xu ở Khám phá). Chọn xong gọi lại báo giá
//    GET api/orders/quote/story/{id}?voucherId= để server tính giá sau giảm; tạo đơn gửi kèm VoucherId.
//  - Mua GÓI: chọn mã giảm % nhận từ CHUỖI ĐỌC (api/me/coupons), đúng số tháng của gói; tạo đơn gửi kèm CouponId.
[QueryProperty(nameof(StoryId), "storyId")]
[QueryProperty(nameof(PlanId), "planId")]
public partial class CheckoutViewModel : BaseViewModel
{
    private readonly IOrderService _orders;
    private readonly IStoryService _stories;
    private readonly IAuthService _auth;
    private readonly IStreakService _streak;
    private readonly RewardsService _rewards;
    private decimal _baseAmount;      // giá gốc trước giảm
    private int? _pendingVoucherId;   // voucher của đơn đang chờ (nếu có)

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

    // ----- Mã giảm giá (voucher mua truyện / mã chuỗi cho gói) -----
    public ObservableCollection<DiscountOption> Discounts { get; } = new();
    [ObservableProperty] private DiscountOption? selectedDiscount;
    [ObservableProperty] private bool hasDiscounts;
    [ObservableProperty] private bool hasNoDiscounts;
    [ObservableProperty] private string discountTitle = string.Empty;
    [ObservableProperty] private string discountHint = string.Empty;
    [ObservableProperty] private string discountText = string.Empty;   // "Giá gốc ... − giảm ..."
    [ObservableProperty] private string originalText = string.Empty;   // giá gốc gạch ngang
    [ObservableProperty] private bool hasSaving;
    [ObservableProperty] private bool isStory;

    public CheckoutViewModel(IOrderService orders, IStoryService stories, IAuthService auth, IStreakService streak,
        RewardsService rewards)
    {
        _orders = orders;
        _stories = stories;
        _auth = auth;
        _streak = streak;
        _rewards = rewards;
    }

    // Hiện giá sau giảm + nội dung nút thanh toán
    private void ShowPrice(decimal finalAmount, decimal discount, string label)
    {
        AmountText = Fmt.Money(finalAmount);
        HasSaving = discount > 0;
        OriginalText = HasSaving ? Fmt.Money(_baseAmount) : string.Empty;
        DiscountText = HasSaving ? $"{label}: −{Fmt.Money(discount)}" : string.Empty;

        var continuePending = _quote?.PendingOrderId is not null
            && (IsStory ? SelectedDiscount?.Id == _pendingVoucherId || (SelectedDiscount?.IsNone != false && _pendingVoucherId is null)
                        : SelectedDiscount?.IsNone != false);
        ConfirmText = continuePending ? "Tiếp tục thanh toán" : $"💳 Thanh toán {AmountText}";
    }

    // Bấm một mã: chưa đủ điều kiện -> báo lý do; còn lại -> chọn và tính lại giá
    [RelayCommand]
    private async Task SelectDiscountAsync(DiscountOption? option)
    {
        if (option is null || _quote is null || option.IsSelected) return;
        if (!option.Eligible)
        {
            await Shell.Current.DisplayAlert("Chưa dùng được mã này", option.Reason, "OK");
            return;
        }

        var previous = SelectedDiscount;
        Select(option);

        if (!IsStory)
        {
            // Gói đọc: giảm % trên giá gói (server tính lại khi tạo đơn)
            var off = option.IsNone ? 0 : Math.Round(_baseAmount * option.Percent / 100m, 0);
            ShowPrice(_baseAmount - off, off, option.IsNone ? string.Empty : $"Mã chuỗi −{option.Percent}%");
            return;
        }

        // Truyện: hỏi lại server giá sau khi áp voucher
        try
        {
            var quote = await _orders.QuoteStoryAsync(StoryId, option.IsNone ? null : option.Id);
            if (quote is null) return;
            _quote.Amount = quote.Amount;
            ShowPrice(quote.Amount, quote.DiscountAmount, "Voucher");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Không áp được voucher", ex.Message, "OK");
            if (previous is not null) Select(previous);
        }
    }

    private void Select(DiscountOption option)
    {
        foreach (var d in Discounts) d.IsSelected = ReferenceEquals(d, option);
        SelectedDiscount = option;
    }

    [RelayCommand]
    private async Task OpenWalletAsync() => await Shell.Current.GoToAsync(nameof(Views.VoucherWalletPage));

    [RelayCommand]
    private async Task OpenShopAsync() => await Shell.Current.GoToAsync("//BrowsePage");

    // Nạp danh sách mã phù hợp với món đang mua
    private async Task LoadDiscountsAsync()
    {
        Discounts.Clear();
        SelectedDiscount = null;
        var none = DiscountOption.None();
        Discounts.Add(none);

        var loggedIn = false;
        try { loggedIn = _auth.CurrentUser is not null || await _auth.IsLoggedInAsync(); } catch { }

        if (IsStory)
        {
            DiscountTitle = "🎟 Voucher mua truyện";
            DiscountHint = "Voucher đổi bằng xu ở Khám phá → Shop đổi voucher. Mỗi đơn dùng 1 voucher.";
            if (loggedIn)
            {
                try
                {
                    var vouchers = await _rewards.GetMyVouchersAsync();
                    foreach (var o in vouchers.Where(v => v.Group == "usable")
                                 .Select(v => DiscountOption.FromVoucher(v, _baseAmount))
                                 .OrderByDescending(o => o.Eligible))
                        Discounts.Add(o);
                }
                catch { /* lỗi tải kho mã: vẫn mua giá gốc được */ }
            }
        }
        else
        {
            DiscountTitle = "🔥 Mã giảm giá gói đọc (từ chuỗi đọc)";
            DiscountHint = "Giữ chuỗi đọc đủ mốc 7 / 30 / 90 / 180 / 365 ngày để nhận mã giảm % cho gói đọc.";
            if (loggedIn)
            {
                try
                {
                    var months = _quote?.DurationMonths ?? 0;
                    foreach (var o in (await _streak.GetCouponsAsync())
                                 .Select(c => DiscountOption.FromCoupon(c, months))
                                 .OrderByDescending(o => o.Eligible))
                        Discounts.Add(o);
                }
                catch { }
            }
        }

        HasDiscounts = Discounts.Count > 1;
        HasNoDiscounts = !HasDiscounts;

        // Đơn đang chờ có voucher -> chọn sẵn voucher đó
        var preselect = IsStory && _pendingVoucherId is int pv
            ? Discounts.FirstOrDefault(d => d.Kind == "Voucher" && d.Id == pv) ?? none
            : none;
        Select(preselect);
        if (IsStory && _quote is not null) ShowPrice(_quote.Amount, _quote.DiscountAmount, "Voucher");
        else ShowPrice(_baseAmount, 0, string.Empty);
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
            IsStory = isStory;
            _quote = isStory ? await _orders.QuoteStoryAsync(StoryId) : await _orders.QuotePlanAsync(PlanId);
            if (_quote is null)
            {
                ErrorMessage = isStory ? "Không tìm thấy truyện." : "Không tìm thấy gói đọc.";
                ShowContent = false;
                return;
            }

            Heading = isStory ? "🛒 Mua trọn bộ truyện" : "🎟 Mua / gia hạn gói đọc";
            _baseAmount = _quote.BaseAmount;
            _pendingVoucherId = _quote.PendingOrderId.HasValue ? _quote.VoucherId : null;
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

            await LoadDiscountsAsync();

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
            var picked = SelectedDiscount is { IsNone: false } d ? d.Id : (int?)null;
            var order = StoryId > 0
                ? await _orders.CreateStoryOrderAsync(StoryId, picked)
                : await _orders.CreatePlanOrderAsync(PlanId, _quote.PlanVersionId, picked);
            RewardsService.NotifyCoinsChanged();

            // "../" = đóng màn Thanh toán rồi mở màn Đơn hàng, nên nút Back từ đơn hàng quay về trang trước đó
            await Shell.Current.GoToAsync($"../{nameof(Views.OrderDetailPage)}", new Dictionary<string, object>
            {
                { "orderId", order.OrderId }
            });
        });
    }
}

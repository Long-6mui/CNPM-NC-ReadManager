using ReadManager.Web.Services;

namespace ReadManager.Web.ViewModels;

// PB21 — trang "Mua trọn bộ": thông tin truyện + giá (lấy từ API) trước khi bấm Thanh toán
public class CheckoutVm
{
    public Story Story { get; set; } = null!;
    public int TotalChapters { get; set; }
    public OrderQuoteApi Quote { get; set; } = null!;
}

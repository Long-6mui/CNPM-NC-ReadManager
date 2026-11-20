using ReadManager.Web.Services;

namespace ReadManager.Web.ViewModels;

// PB21 — trang "Mua trọn bộ": thông tin truyện + giá (lấy từ API) trước khi bấm Thanh toán
public class CheckoutVm
{
    public Story Story { get; set; } = null!;
    public int TotalChapters { get; set; }
    public OrderQuoteApi Quote { get; set; } = null!;

    // Voucher của tài khoản (API: api/me/vouchers?status=Available) + voucher đang được chọn
    public List<VoucherApi> Vouchers { get; set; } = [];
    public int? SelectedVoucherId { get; set; }
    public string? VoucherError { get; set; }
    public bool VouchersUnavailable { get; set; }

    // Giá hiển thị: Quote.Amount là số tiền phải trả (backend đã tự tính giảm giá)
    public decimal DiscountAmount => Quote.DiscountAmount;
    public decimal OriginalAmount => Quote.OriginalAmount ?? Quote.Amount + Quote.DiscountAmount;
    public bool HasDiscount => SelectedVoucherId.HasValue && Quote.DiscountAmount > 0;
}

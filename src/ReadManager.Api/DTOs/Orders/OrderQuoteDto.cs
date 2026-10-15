namespace ReadManager.Api.DTOs.Orders;

// BÁO GIÁ trước khi mua — để Web/Mobile hiện nút "Mua — 50.000₫" / "Đã sở hữu" / "Tiếp tục thanh toán"
public class OrderQuoteDto
{
    public string OrderType { get; set; } = string.Empty;
    public int? StoryId { get; set; }
    public int? PlanId { get; set; }
    public int? PlanVersionId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? DurationMonths { get; set; }

    public bool CanBuy { get; set; }             // true = được tạo đơn
    public bool AlreadyOwned { get; set; }       // đã mua truyện này rồi
    public int? PendingOrderId { get; set; }     // đang có đơn chờ thanh toán → mở lại đơn đó
    public string? Message { get; set; }         // lý do không mua được (vd: "Truyện miễn phí")
}

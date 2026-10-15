namespace ReadManager.Api.DTOs.Orders;

// 1 ĐƠN HÀNG trả về cho Web/Mobile (chi tiết đơn + 1 dòng trong lịch sử thanh toán)
public class OrderDto
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }          // chỉ điền ở danh sách của Admin

    public string OrderType { get; set; } = string.Empty;   // Plan | Story
    public int? PlanId { get; set; }
    public int? PlanVersionId { get; set; }
    public int? StoryId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? DurationMonths { get; set; }

    public string Status { get; set; } = string.Empty;      // Pending | Paid | Failed | Cancelled | Expired
    public DateTime CreatedAt { get; set; }                  // giờ UTC
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiresAt { get; set; }                 // đơn Pending: hạn chót thanh toán

    public bool CanPay { get; set; }       // true = còn thanh toán được (Pending, chưa hết hạn)
}

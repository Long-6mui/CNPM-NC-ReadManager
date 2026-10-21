namespace ReadManager.Api.Entities;

// BE3 — ĐƠN HÀNG (PB19 mua/gia hạn gói, PB21 mua truyện, PB26 lịch sử thanh toán)
// Mỗi đơn mua đúng 1 thứ: 1 gói đọc (OrderType = "Plan") HOẶC 1 truyện (OrderType = "Story").
// Giá, tên món hàng, số tháng được CHỤP LẠI lúc tạo đơn → sau này Admin đổi giá thì đơn cũ không đổi.
//
// Status:
//   "Pending"   = chờ thanh toán (mới tạo)
//   "Paid"      = đã thanh toán thành công (phần Thanh toán — BE4 — cập nhật)
//   "Failed"    = thanh toán thất bại
//   "Cancelled" = đơn bị hủy (phần Thanh toán — BE4 — xử lý)
//   "Expired"   = quá 30 phút không thanh toán
public class Order
{
    public int OrderId { get; set; }
    public int UserId { get; set; }                 // người mua

    public string OrderType { get; set; } = string.Empty;   // "Plan" | "Story"
    public int? PlanVersionId { get; set; }         // mua gói → phiên bản gói lúc mua
    public int? StoryId { get; set; }               // mua truyện → truyện được mua

    public string ItemName { get; set; } = string.Empty;    // tên món hàng lúc mua (vd: "Gói Tháng (1 tháng)")
    public decimal Amount { get; set; }             // số tiền phải trả, lấy từ DB lúc tạo đơn
    public int? DurationMonths { get; set; }        // mua gói → số tháng lúc mua

    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }           // lúc thanh toán thành công

    public User User { get; set; } = null!;
    public ReadingPlanVersion? PlanVersion { get; set; }
    public Story? Story { get; set; }
}

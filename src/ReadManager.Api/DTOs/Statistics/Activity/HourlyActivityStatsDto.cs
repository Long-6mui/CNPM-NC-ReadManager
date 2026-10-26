namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 (Mở rộng thực tế) — Thống kê theo khung giờ trong ngày (0h..23h giờ Việt Nam)
// Giúp Admin / Tác giả biết "Giờ vàng" độc giả đọc truyện & mua gói nhiều nhất để hẹn giờ đăng chương.
public class HourlyActivityStatsDto
{
    public int Hour { get; set; }                  // 0 .. 23 (giờ Việt Nam)
    public string Label { get; set; } = string.Empty; // vd: "21:00 - 21:59"
    public int Reads { get; set; }                 // Số lượt đọc chương trong khung giờ này
    public int UniqueReaders { get; set; }         // Số độc giả khác nhau đọc trong khung giờ này
    public int Comments { get; set; }              // Số bình luận trong khung giờ này
    public int PaidOrders { get; set; }            // Số giao dịch thành công trong khung giờ này
    public bool IsPeakHour { get; set; }           // true = thuộc nhóm khung giờ cao điểm nhất
}

namespace ReadManager.Api.DTOs.Statistics.Content;

// Bộ lọc dùng chung cho các API thống kê nội dung (gửi trên query string)
//   ?from=2026-10-01&to=2026-10-21   (ngày theo giờ Việt Nam, tính cả 2 đầu)
//   Bỏ trống → mặc định 30 ngày gần nhất. Tối đa 366 ngày.
public class ContentStatsQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    // Chỉ dùng cho danh sách truyện:
    //   sort = interest (mặc định) | reads | follows | purchases | comments
    //   top  = số truyện trả về (1–100, mặc định 10)
    public string? Sort { get; set; }
    public int Top { get; set; } = 10;
}

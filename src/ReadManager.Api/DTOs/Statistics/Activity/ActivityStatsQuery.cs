namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 — Bộ lọc thời gian dùng chung cho các API thống kê hoạt động (gửi trên query string)
//   ?from=2026-10-01&to=2026-10-25   (ngày theo giờ Việt Nam, tính cả 2 đầu)
//   Bỏ trống → mặc định 30 ngày gần nhất. Tối đa 366 ngày.
public class ActivityStatsQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    // Dùng cho top-readers hoặc recent-feed:
    //   sort = score (mặc định) | reads | spent | comments
    //   top  = số dòng trả về (1–100, mặc định 10)
    public string? Sort { get; set; }
    public int Top { get; set; } = 10;
}

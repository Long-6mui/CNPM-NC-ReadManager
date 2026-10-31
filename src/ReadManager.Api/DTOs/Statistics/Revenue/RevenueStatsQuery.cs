namespace ReadManager.Api.DTOs.Statistics.Revenue;

// PB38 + PB39 — Bộ lọc dùng chung cho các API thống kê doanh thu (gửi trên query string)
//   ?from=2026-10-01&to=2026-10-30      ngày theo giờ Việt Nam, tính cả 2 đầu. Bỏ trống = 30 ngày gần nhất, tối đa 366 ngày.
//   &type=all|plan|story                 tất cả (mặc định) / gói đọc / mua truyện
//   &planVersionId=5                     chỉ 1 phiên bản gói (tự hiểu là type=plan)
//   &storyId=12                          chỉ 1 truyện (tự hiểu là type=story)
//   &groupBy=day|week|month              chỉ dùng cho /series. Tuần bắt đầu từ thứ Hai.
//   &top=20                              chỉ dùng cho /stories (1–100)
// Không gửi cùng lúc planVersionId và storyId; type=story đi với planVersionId (hoặc type=plan đi với storyId) là sai → 400.
public class RevenueStatsQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Type { get; set; }
    public int? PlanVersionId { get; set; }
    public int? StoryId { get; set; }
    public string? GroupBy { get; set; }
    public int Top { get; set; } = 20;
}

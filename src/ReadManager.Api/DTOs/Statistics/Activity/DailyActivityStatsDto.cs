namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 — Số liệu hoạt động theo từng ngày (theo múi giờ Việt Nam) để vẽ biểu đồ
public class DailyActivityStatsDto
{
    public DateOnly Date { get; set; }
    public int NewMembers { get; set; }
    public int ActiveReaders { get; set; }      // DAU (Daily Active Readers) — số độc giả đọc truyện trong ngày
    public int ChaptersRead { get; set; }       // Tổng số lượt đọc chương trong ngày
    public int NewSubscriptions { get; set; }
    public int Renewals { get; set; }
    public int StoryPurchases { get; set; }
    public int TotalOrders { get; set; }
    public int PaidOrders { get; set; }
    public int PendingOrders { get; set; }
    public int FailedOrders { get; set; }
}

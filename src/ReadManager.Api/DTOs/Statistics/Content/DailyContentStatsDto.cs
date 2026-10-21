namespace ReadManager.Api.DTOs.Statistics.Content;

// PB40 — SỐ LIỆU TỪNG NGÀY (để Web vẽ biểu đồ đường). Ngày theo giờ Việt Nam.
public class DailyContentStatsDto
{
    public DateOnly Date { get; set; }
    public int Reads { get; set; }
    public int NewFollows { get; set; }
    public int Purchases { get; set; }
    public int Comments { get; set; }
}

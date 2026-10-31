namespace ReadManager.Api.DTOs.Statistics.Revenue;

// PB39 — Lượt mua và doanh thu theo từng truyện (chỉ đơn mua truyện; tiền mua gói không chia xuống truyện)
public class StoryRevenueDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Purchases { get; set; }
    public decimal Revenue { get; set; }
    public double SharePercent { get; set; }           // % trên tổng doanh thu mua truyện trong danh sách
}

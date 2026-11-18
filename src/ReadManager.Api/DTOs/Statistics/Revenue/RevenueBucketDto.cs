namespace ReadManager.Api.DTOs.Statistics.Revenue;

// PB38 + PB39 — Doanh thu theo từng mốc ngày / tuần / tháng để vẽ biểu đồ. Mốc không có giao dịch vẫn có mặt với số 0.
// PeriodStart/PeriodEnd là phần của mốc nằm TRONG khoảng đang xem (tuần/tháng ở 2 đầu khoảng có thể bị cắt ngắn).
public class RevenueBucketDto
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal PlanRevenue { get; set; }
    public decimal StoryRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    public int Orders { get; set; }
}

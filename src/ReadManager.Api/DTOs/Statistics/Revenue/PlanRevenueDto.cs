namespace ReadManager.Api.DTOs.Statistics.Revenue;

// PB38 — Doanh thu theo từng PHIÊN BẢN gói đọc (ReadingPlanVersion), có tách mua mới / gia hạn
public class PlanRevenueDto
{
    public int PlanVersionId { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public int DurationMonths { get; set; }
    public int Orders { get; set; }
    public int NewOrders { get; set; }
    public int RenewalOrders { get; set; }
    public decimal Revenue { get; set; }
    public decimal NewRevenue { get; set; }
    public decimal RenewalRevenue { get; set; }
    public double SharePercent { get; set; }           // % trên tổng doanh thu gói trong danh sách
}

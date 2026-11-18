namespace ReadManager.Api.DTOs.Statistics.Revenue;

// PB38 + PB39 — Tổng quan doanh thu trong khoảng [From, To] (ngày thanh toán, giờ Việt Nam)
// Doanh thu = tổng Orders.Amount của đơn Paid. Tiền của gói thuộc doanh thu gói, không cộng vào doanh thu từng truyện.
public class RevenueOverviewDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Type { get; set; } = "all";          // bộ lọc loại đã áp dụng: all | plan | story

    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }
    public decimal AverageOrderValue { get; set; }

    // ----- Gói đọc -----
    public decimal PlanRevenue { get; set; }
    public int PlanOrders { get; set; }
    public decimal NewPlanRevenue { get; set; }        // mua mới
    public int NewPlanOrders { get; set; }
    public decimal RenewalRevenue { get; set; }        // gia hạn: mua lại cùng gói khi gói cũ còn hạn
    public int RenewalOrders { get; set; }

    // ----- Mua truyện -----
    public decimal StoryRevenue { get; set; }
    public int StoryOrders { get; set; }
}

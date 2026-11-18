namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 — Thống kê chi tiết từng trạng thái giao dịch (Đơn hàng & Thanh toán)
public class TransactionStatusStatsDto
{
    public string Status { get; set; } = string.Empty; // Paid | Pending | Failed | Cancelled | Expired
    public int OrderCount { get; set; }
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public double Percentage { get; set; }
}

public class TransactionStatusSummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalOrders { get; set; }
    public int TotalPayments { get; set; }
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledCount { get; set; }
    public int ExpiredCount { get; set; }
    public double SuccessRate { get; set; }
    public List<TransactionStatusStatsDto> StatusBreakdown { get; set; } = new();
}

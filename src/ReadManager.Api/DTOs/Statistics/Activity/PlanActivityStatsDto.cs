namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 — Thống kê đăng ký mới, gia hạn và số thành viên đang dùng theo từng gói đọc
public class PlanActivityStatsDto
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int NewSubscriptions { get; set; }
    public int Renewals { get; set; }
    public int TotalPurchases { get; set; }
    public int ActiveSubscribers { get; set; }
    public int ExpiringSoonCount { get; set; }  // Số thuê bao sắp hết hạn trong 7 ngày tới
    public int ExpiredCount { get; set; }       // Số thuê bao đã hết hạn
    public double RenewalRate { get; set; }     // Tỷ lệ gia hạn (%)
}

namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 — TỔNG QUAN thống kê hoạt động thành viên, đăng ký mới, gia hạn, mua truyện và trạng thái giao dịch
public class ActivityOverviewDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    // ----- 1. Thành viên & Độc giả -----
    public int TotalMembers { get; set; }
    public int ActiveAccounts { get; set; }
    public int LockedAccounts { get; set; }
    public int NewMembers { get; set; }
    public int ActiveReaders { get; set; }
    public int ActiveSubscribers { get; set; }
    public int ExpiringSoonSubscriptions { get; set; } // Gói sắp hết hạn trong 7 ngày tới (cần nhắc gia hạn)
    public int ChurnedSubscriptions { get; set; }      // Người dùng đã hết hạn gói nhưng chưa gia hạn lại

    // ----- 2. Đăng ký gói, gia hạn & mua truyện (trong khoảng [From, To]) -----
    public int NewSubscriptions { get; set; }
    public int Renewals { get; set; }
    public int TotalPlanOrdersPaid { get; set; }
    public int StoryPurchases { get; set; }
    public int UniqueBuyers { get; set; }

    // ----- 3. Chỉ số tăng trưởng (% so với kỳ liền trước có cùng số ngày) & Tỷ lệ vận hành -----
    public double NewMembersGrowthPercent { get; set; }
    public double ActiveReadersGrowthPercent { get; set; }
    public double SubscriptionsGrowthPercent { get; set; }
    public double StoryPurchasesGrowthPercent { get; set; }
    public double RenewalRate { get; set; }                // Tỷ lệ gia hạn trong kỳ (%)
    public double ConversionRate { get; set; }             // Tỷ lệ chuyển đổi từ Độc giả → Người mua trả phí (%)
    public double AverageChaptersPerReader { get; set; }   // Trung bình số chương đọc / độc giả hoạt động
    public int? PeakReadingHour { get; set; }              // Khung giờ vàng có lượt đọc cao nhất (0..23h giờ VN)

    // ----- 4. Trạng thái giao dịch (trong khoảng [From, To]) -----
    public int TotalOrders { get; set; }
    public int PaidOrders { get; set; }
    public int PendingOrders { get; set; }
    public int FailedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int ExpiredOrders { get; set; }
    public double SuccessRate { get; set; }

    // ----- 5. Chi tiết phân bổ & Bảng thực tế -----
    public List<TransactionStatusStatsDto> TransactionsByStatus { get; set; } = new();
    public List<PlanActivityStatsDto> Plans { get; set; } = new();
    public List<TopReaderActivityDto> TopReaders { get; set; } = new();
    public List<RecentActivityEventDto> RecentActivities { get; set; } = new();
}

namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 — Thống kê chi tiết hoạt động thành viên & giữ chân thuê bao
public class MemberActivitySummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalUsers { get; set; }
    public int TotalMembers { get; set; }
    public int TotalAdmins { get; set; }
    public int ActiveAccounts { get; set; }
    public int LockedAccounts { get; set; }
    public int NewRegistrations { get; set; }
    public int ActiveReadersInPeriod { get; set; }
    public int ActiveSubscribersNow { get; set; }
    public int ExpiringSoonSubscribers { get; set; } // Thuê bao sắp hết hạn trong 7 ngày tới
    public int ChurnedSubscribers { get; set; }      // Thuê bao từng mua gói nhưng đã hết hạn chưa gia hạn lại
    public int TotalStoryOwners { get; set; }
    public double PayingMemberRate { get; set; }     // % thành viên đã từng chi tiêu (mua gói hoặc mua truyện)
    public double RetentionRate { get; set; }        // % giữ chân thuê bao (ActiveSubscribers / Tổng người từng mua gói)
    public double AverageChaptersPerReader { get; set; } // Trung bình số chương đọc / mỗi độc giả hoạt động
}

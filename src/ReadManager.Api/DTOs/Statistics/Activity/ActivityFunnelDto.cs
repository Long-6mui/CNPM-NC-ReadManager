namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 (Mở rộng thực tế) — Phễu chuyển đổi người dùng (Conversion Funnel)
// Từ Đăng ký / Thành viên → Đọc chương → Tương tác (Follow/Comment) → Tạo đơn hàng → Thanh toán thành công
public class FunnelStageDto
{
    public string StageCode { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public double PercentageOfTotal { get; set; }     // % so với đỉnh phễu
    public double ConversionFromPrevious { get; set; } // % chuyển đổi từ bước ngay trước đó
}

public class ActivityFunnelDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalActiveMembers { get; set; }
    public int ReadersCount { get; set; }
    public int EngagedUsersCount { get; set; }
    public int CheckoutUsersCount { get; set; }
    public int PayingUsersCount { get; set; }
    public double OverallConversionRate { get; set; } // PayingUsers / Readers (%)
    public List<FunnelStageDto> Stages { get; set; } = new();
}

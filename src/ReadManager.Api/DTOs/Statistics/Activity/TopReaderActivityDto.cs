namespace ReadManager.Api.DTOs.Statistics.Activity;

// PB37 (Mở rộng thực tế) — Bảng xếp hạng Độc giả tích cực / Top VIP ủng hộ trên nền tảng
public class TopReaderActivityDto
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    // Phân hạng tự động: "VIP Thuê Bao" | "Người Mua Truyện" | "Độc Giả Tích Cực" | "Thành Viên Mới"
    public string MemberTier { get; set; } = string.Empty;
    public bool HasActiveSubscription { get; set; }

    public int ChaptersRead { get; set; }
    public int StoriesRead { get; set; }
    public int CommentsPosted { get; set; }
    public int StoriesFollowed { get; set; }
    public int PaidOrdersCount { get; set; }
    public decimal TotalSpent { get; set; }

    // Điểm hoạt động tổng hợp = ChaptersRead * 1 + CommentsPosted * 3 + StoriesFollowed * 5 + PaidOrdersCount * 20
    public int ActivityScore { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

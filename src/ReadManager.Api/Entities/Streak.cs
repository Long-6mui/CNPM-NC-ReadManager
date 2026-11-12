namespace ReadManager.Api.Entities;

// SPRINT 5 — CHUỖI ĐỌC SÁCH (Reading Streak)
// Ngày được tính theo GIỜ VIỆT NAM (UTC+7). Đọc >= 1 chương trong ngày = có đọc ngày đó.

// Trạng thái chuỗi của 1 người dùng (1 dòng / người).
[Microsoft.EntityFrameworkCore.PrimaryKey(nameof(UserId), nameof(RequestId))]
public class FreezeCardPurchase
{
    public int UserId { get; set; }
    public Guid RequestId { get; set; }
    public int Cost { get; set; }
    public User User { get; set; } = null!;
}

public class AdminReaderGrant
{
    [System.ComponentModel.DataAnnotations.Key]
    public Guid RequestId { get; set; }
    public int AdminId { get; set; }
    public int UserId { get; set; }
    public int Coins { get; set; }
    public int Days { get; set; }
    public DateTime GrantedAt { get; set; }
}

public class UserStreak
{
    public int UserId { get; set; }                 // khóa chính, cũng là khóa ngoại tới Users

    public int CurrentStreak { get; set; }          // chuỗi hiện tại (ngày)
    public int EarnedStreakCoins { get; set; }
    public int LongestStreak { get; set; }          // chuỗi dài nhất từng đạt
    public DateOnly? LastReadDate { get; set; }     // ngày (giờ VN) đọc gần nhất được tính vào chuỗi

    public int FreezeCards { get; set; }            // số Thẻ bảo vệ chuỗi đang có
    public int RestoresUsed { get; set; }           // số lần đã khôi phục chuỗi (tối đa 5)

    public int BrokenStreak { get; set; }           // độ dài chuỗi vừa mất (> 0 = còn có thể khôi phục)
    public DateTime? BrokenAt { get; set; }         // lúc chuỗi bị đứt (UTC) — khôi phục trong 72 giờ

    public string? SelectedTitle { get; set; }      // danh hiệu đang đeo (hiện cạnh tên trong bình luận)
    public string? SelectedFrame { get; set; }      // khung avatar đang đeo

    public DateOnly? LastWarnDate { get; set; }     // ngày đã gửi thông báo "sắp mất chuỗi" (tránh gửi lặp)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

// Phần thưởng đã nhận (huy hiệu / danh hiệu / khung avatar / đặc quyền / thẻ / coupon — để lưu lịch sử).
public class StreakReward
{
    public int StreakRewardId { get; set; }
    public int UserId { get; set; }
    public int Milestone { get; set; }              // mốc ngày: 7, 30, 90, 180, 365
    public string Kind { get; set; } = string.Empty;   // Badge | Title | Frame | Freeze | Coupon | Perk
    public string Code { get; set; } = string.Empty;   // mã định danh (vd: "frame_dong")
    public string Name { get; set; } = string.Empty;   // tên hiển thị
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

// Mã giảm giá nhận từ chuỗi. Áp vào đơn mua GÓI ĐỌC có số tháng phù hợp.
// Coupon "đang dùng" = có đơn Pending/Paid trỏ tới nó (không cần cột trạng thái riêng).
public class Coupon
{
    public int CouponId { get; set; }
    public int UserId { get; set; }
    public int Milestone { get; set; }
    public int Percent { get; set; }                // % giảm (5, 10, 15, 20, 35)
    public string DurationMonthsCsv { get; set; } = string.Empty;   // gói áp dụng, vd "1" hoặc "6,12"
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public User User { get; set; } = null!;
}

// Thông báo về chuỗi (không dùng bảng Notifications vì bảng đó bắt buộc gắn với 1 truyện).
public class StreakNotice
{
    public int StreakNoticeId { get; set; }
    public int UserId { get; set; }
    public string Kind { get; set; } = string.Empty;   // AtRisk | Broken | FreezeUsed | Milestone | Restored
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

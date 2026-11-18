namespace ReadManager.Api.DTOs.Streak;

public class StreakDayDto
{
    public string Date { get; set; } = string.Empty;   // "yyyy-MM-dd" (giờ VN)
    public string Label { get; set; } = string.Empty;  // "T2".."CN"
    public bool Read { get; set; }
    public bool IsToday { get; set; }
}

public class StreakRewardItemDto
{
    public string Kind { get; set; } = string.Empty;   // Badge | Title | Frame | Freeze | Coupon | Perk
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Unlocked { get; set; }                 // false = hiển thị mờ
}

public class StreakMilestoneDto
{
    public int Days { get; set; }
    public string Name { get; set; } = string.Empty;   // "Làm quen thói quen"
    public bool Reached { get; set; }
    public List<StreakRewardItemDto> Rewards { get; set; } = new();
}

public class StreakDto
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool ReadToday { get; set; }
    public int FreezeCards { get; set; }

    public int RestoresUsed { get; set; }
    public int RestoresLeft { get; set; }              // 5 - RestoresUsed
    public bool CanRestore { get; set; }
    public int BrokenStreak { get; set; }              // chuỗi vừa mất (nếu còn khôi phục được)
    public DateTime? RestoreDeadline { get; set; }     // UTC

    public string IconTier { get; set; } = "orange";   // orange | darkred (từ mốc 90 ngày)
    public string? SelectedTitle { get; set; }
    public string? SelectedFrame { get; set; }

    public int NextMilestoneDays { get; set; }         // 0 = đã đạt hết
    public string? NextMilestoneName { get; set; }
    public int UnreadNotices { get; set; }

    public List<StreakDayDto> Last7Days { get; set; } = new();
    public List<StreakMilestoneDto> Milestones { get; set; } = new();
    public List<StreakRewardItemDto> Collection { get; set; } = new();   // huy hiệu + danh hiệu + khung (cả mờ)
}

public class UpdateAppearanceDto
{
    public string? Title { get; set; }    // null/"" = bỏ đeo
    public string? Frame { get; set; }
}

public class CouponDto
{
    public int CouponId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Percent { get; set; }
    public List<int> DurationMonths { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

public class StreakNoticeDto
{
    public int StreakNoticeId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StreakLeaderboardItemDto
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Title { get; set; }
    public string? Frame { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool Medal { get; set; }                    // huy chương vinh danh (đạt mốc 365 ngày)
}

using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// ===================== SPRINT 5: CHUỖI ĐỌC SÁCH =====================
// Tên trường khớp DTO của API: StreakDto / StreakDayDto / StreakMilestoneDto / StreakRewardItemDto / CouponDto / StreakNoticeDto.

public static class StreakLook
{
    // Mã khung avatar -> tên + màu viền
    public static string FrameName(string? code) => code switch
    {
        "frame_dong" => "Ngọn Lửa Đồng",
        "frame_bac" => "Ngọn Lửa Bạc",
        "frame_kim_cuong" => "Lửa Kim Cương",
        _ => "Không đeo"
    };

    public static Color FrameColor(string? code) => code switch
    {
        "frame_dong" => Color.FromArgb("#B87333"),
        "frame_bac" => Color.FromArgb("#9AA5B1"),
        "frame_kim_cuong" => Color.FromArgb("#22D3EE"),
        _ => Colors.Transparent
    };

    public static double FrameThickness(string? code) => string.IsNullOrEmpty(code) ? 0 : 3;
}

public class StreakDay
{
    public string Date { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Read { get; set; }
    public bool IsToday { get; set; }

    public string Mark => Read ? "🔥" : (IsToday ? "·" : "○");
    public Color BgColor => Read ? Color.FromArgb("#FFF1E0") : Color.FromArgb("#EEF2F9");
    public Color BorderColor => IsToday ? Color.FromArgb("#1D4ED8") : Colors.Transparent;
}

public class StreakRewardItem
{
    public string Kind { get; set; } = string.Empty;   // Badge | Title | Frame | Freeze | Coupon | Perk
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Unlocked { get; set; }

    public string Icon => Kind switch
    {
        "Badge" => "🏅",
        "Title" => "🏷",
        "Frame" => "🖼",
        "Freeze" => "🧊",
        "Coupon" => "🎟",
        "Perk" => "⭐",
        _ => "🎁"
    };
    public string Display => $"{Icon} {Name}";
    public double ItemOpacity => Unlocked ? 1.0 : 0.35;      // chưa đạt: hiển thị mờ
    public string StateText => Unlocked ? "Đã mở khóa" : "Chưa mở khóa";
    public bool IsTitle => Kind == "Title";
    public bool IsFrame => Kind == "Frame";
    public bool CanEquip => Unlocked && (IsTitle || IsFrame);
}

public class StreakMilestone
{
    public int Days { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Reached { get; set; }
    public List<StreakRewardItem> Rewards { get; set; } = new();

    public string Header => $"{Days} ngày · {Name}";
    public string Mark => Reached ? "✅" : "🔒";
    public double ItemOpacity => Reached ? 1.0 : 0.55;
}

public class StreakInfo
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool ReadToday { get; set; }
    public int FreezeCards { get; set; }
    public int RestoresUsed { get; set; }
    public int RestoresLeft { get; set; }
    public bool CanRestore { get; set; }
    public int BrokenStreak { get; set; }
    public DateTime? RestoreDeadline { get; set; }
    public string IconTier { get; set; } = "orange";   // orange | darkred
    public string? SelectedTitle { get; set; }
    public string? SelectedFrame { get; set; }
    public int NextMilestoneDays { get; set; }
    public string? NextMilestoneName { get; set; }
    public int UnreadNotices { get; set; }
    public List<StreakDay> Last7Days { get; set; } = new();
    public List<StreakMilestone> Milestones { get; set; } = new();
    public List<StreakRewardItem> Collection { get; set; } = new();

    // ----- Chỉ để hiển thị -----
    public Color FlameColor => IconTier == "darkred" ? Color.FromArgb("#9B1C1C") : Color.FromArgb("#F97316");
    public string ChipText => $"🔥 {CurrentStreak}";
    public string RestoreText => RestoreDeadline.HasValue
        ? $"Khôi phục chuỗi {BrokenStreak} ngày (còn {RestoresLeft}/5 lượt) — hạn {Fmt.Local(RestoreDeadline.Value):HH:mm dd/MM}"
        : $"Còn {RestoresLeft}/5 lượt khôi phục";
    public string StatusText => ReadToday
        ? "Hôm nay bạn đã đọc — chuỗi được giữ ✔"
        : CurrentStreak > 0 ? "Hôm nay bạn chưa đọc chương nào. Đọc 1 chương để giữ chuỗi!" : "Đọc 1 chương hôm nay để bắt đầu chuỗi mới.";
    public string NextText => NextMilestoneDays > 0
        ? $"Mốc tiếp theo: {NextMilestoneDays} ngày ({NextMilestoneName}) — còn {Math.Max(0, NextMilestoneDays - CurrentStreak)} ngày"
        : "Bạn đã đạt tất cả các mốc. Huyền thoại!";
    public Color SelectedFrameColor => StreakLook.FrameColor(SelectedFrame);
    public double SelectedFrameThickness => StreakLook.FrameThickness(SelectedFrame);
    public bool IsDiamondFrame => SelectedFrame == "frame_kim_cuong";
    public bool HasTitle => !string.IsNullOrEmpty(SelectedTitle);
    public string TitleText => HasTitle ? $"Danh hiệu: {SelectedTitle}" : "Chưa đeo danh hiệu";
    public string FrameText => $"Khung avatar: {StreakLook.FrameName(SelectedFrame)}";
}

public class EquipRequest
{
    public string? Title { get; set; }
    public string? Frame { get; set; }
}

// GET api/me/coupons
public class StreakCoupon
{
    public int CouponId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Percent { get; set; }
    public List<int> DurationMonths { get; set; } = new();
    public DateTime ExpiresAt { get; set; }

    public string Display => $"{Name} · hạn {Fmt.Local(ExpiresAt):dd/MM/yyyy}";
}

// GET api/me/streak/notices
public class StreakNotice
{
    public int StreakNoticeId { get; set; }
    public string Kind { get; set; } = string.Empty;   // AtRisk | Broken | FreezeUsed | Milestone | Restored
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public string Icon => Kind switch
    {
        "AtRisk" => "⏰", "Broken" => "💔", "FreezeUsed" => "🧊", "Milestone" => "🎉", "Restored" => "🔄", _ => "🔥"
    };
    public string TimeText => TimeAgo.Of(CreatedAt);
    public double ItemOpacity => IsRead ? 0.6 : 1.0;
}

// GET api/ranking/streak
public class StreakRankItem
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Frame { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool Medal { get; set; }

    public string Line1 => $"#{Rank}  {DisplayName}{(Medal ? " 🏆" : "")}";
    public string Line2 => $"{(string.IsNullOrEmpty(Title) ? "" : Title + " · ")}Dài nhất {LongestStreak} ngày · hiện tại {CurrentStreak}";
}

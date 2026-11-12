namespace ReadManager.Api.Services;

// DANH MỤC MỐC CHUỖI & PHẦN THƯỞNG — sửa số liệu ở đây là đổi toàn bộ hệ thống.
public record StreakRewardDef(string Kind, string Code, string Name);

public record StreakCouponDef(int Percent, int[] DurationMonths, string Name);

public record StreakMilestoneDef(
    int Days, string Name,
    int FreezeCards,
    StreakCouponDef? Coupon,
    StreakRewardDef[] Rewards);

public static class StreakCatalog
{
    public const int MaxRestores = 5;                         // tối đa 5 lần khôi phục
    public static readonly TimeSpan RestoreWindow = TimeSpan.FromHours(72);   // khôi phục trong 72 giờ
    public const int MaxFreezeCards = 5;                      // giữ tối đa 5 thẻ
    public const int CouponLifetimeDays = 90;                 // coupon dùng được 90 ngày
    public const int IconDarkRedFrom = 90;                    // từ mốc 90 ngày icon chuyển đỏ đậm
    public const int MedalFrom = 365;

    public static readonly StreakMilestoneDef[] Milestones =
    {
        new(7, "Làm quen thói quen", 1,
            new StreakCouponDef(5, new[] { 1 }, "Giảm 5% Gói đọc tháng"),
            new[] { new StreakRewardDef("Badge", "badge_khoi_dau_moi", "Khởi Đầu Mới") }),

        new(30, "Độc giả chăm chỉ", 0,
            new StreakCouponDef(10, new[] { 1 }, "Giảm 10% Gói đọc tháng"),
            new[] { new StreakRewardDef("Title", "title_mot_sach", "Mọt Sách Tập Sự") }),

        new(90, "Người đam mê", 0,
            new StreakCouponDef(15, new[] { 6 }, "Giảm 15% Gói đọc 6 tháng"),
            new[] { new StreakRewardDef("Frame", "frame_dong", "Ngọn Lửa Đồng"),
                    new StreakRewardDef("Perk", "perk_icon_do", "Icon chuỗi đỏ đậm") }),

        new(180, "Chuyên gia cày truyện", 3,
            new StreakCouponDef(20, new[] { 6, 12 }, "Giảm 20% Gói đọc 6 tháng / năm"),
            new[] { new StreakRewardDef("Frame", "frame_bac", "Ngọn Lửa Bạc"),
                    new StreakRewardDef("Title", "title_tang_kinh_cac", "Tàng Kinh Các"),
                    new StreakRewardDef("Title", "title_hoc_gia", "Học Giả") }),

        new(365, "Huyền thoại", 0,
            new StreakCouponDef(35, new[] { 12 }, "Giảm 35% Gói đọc năm"),
            new[] { new StreakRewardDef("Frame", "frame_kim_cuong", "Lửa Kim Cương"),
                    new StreakRewardDef("Title", "title_than_dong", "Thần Đồng Gác Sách"),
                    new StreakRewardDef("Perk", "perk_medal", "Huy chương vinh danh Bảng xếp hạng") }),
    };

    // Phần thưởng hiển thị trong "Bộ sưu tập": huy hiệu / danh hiệu / khung
    public static IEnumerable<(int Milestone, StreakRewardDef Def)> CollectionItems()
        => Milestones.SelectMany(m => m.Rewards
            .Where(r => r.Kind is "Badge" or "Title" or "Frame")
            .Select(r => (m.Days, r)));

}

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

    // Mốc chuỗi: giữ chuỗi ĐỦ số ngày này là nhận thưởng (mỗi mốc nhận 1 lần).
    // Khung / danh hiệu đẹp dần theo mốc. Mã (Code) cũ giữ nguyên để không mất đồ của người đã nhận.
    public static readonly StreakMilestoneDef[] Milestones =
    {
        new(3, "Khởi động", 0, null,
            new[] { new StreakRewardDef("Frame", "frame_dom_lua", "Đốm Lửa"),
                    new StreakRewardDef("Title", "title_cham_chi", "Chăm Chỉ Mỗi Ngày") }),

        new(7, "Làm quen thói quen", 1,
            new StreakCouponDef(5, new[] { 1 }, "Giảm 5% Gói đọc tháng"),
            new[] { new StreakRewardDef("Badge", "badge_khoi_dau_moi", "Khởi Đầu Mới"),
                    new StreakRewardDef("Frame", "frame_lua_hong", "Lửa Hồng") }),

        new(14, "Giữ nhịp", 0, null,
            new[] { new StreakRewardDef("Frame", "frame_luc_bao", "Lục Bảo"),
                    new StreakRewardDef("Title", "title_san_chuong", "Kẻ Săn Chương") }),

        new(30, "Độc giả chăm chỉ", 0,
            new StreakCouponDef(10, new[] { 1 }, "Giảm 10% Gói đọc tháng"),
            new[] { new StreakRewardDef("Title", "title_mot_sach", "Mọt Sách Tập Sự"),
                    new StreakRewardDef("Frame", "frame_hoang_hon", "Hoàng Hôn") }),

        new(60, "Bền bỉ", 1, null,
            new[] { new StreakRewardDef("Frame", "frame_thach_anh", "Thạch Anh Tím"),
                    new StreakRewardDef("Title", "title_giu_lua", "Người Giữ Lửa") }),

        new(90, "Người đam mê", 0,
            new StreakCouponDef(15, new[] { 6 }, "Giảm 15% Gói đọc 6 tháng"),
            new[] { new StreakRewardDef("Frame", "frame_dong", "Ngọn Lửa Đồng"),
                    new StreakRewardDef("Title", "title_dam_me", "Người Đam Mê"),
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

    // Khung / danh hiệu cho NGƯỜI MỚI: ai cũng dùng được ngay, kiểu dáng đơn giản
    public static readonly StreakRewardDef[] StarterItems =
    {
        new("Frame", "frame_moc", "Gỗ Mộc"),
        new("Frame", "frame_may", "Mây Trắng"),
        new("Frame", "frame_la_non", "Lá Non"),
        new("Title", "title_tan_doc_gia", "Tân Độc Giả"),
        new("Title", "title_nguoi_moi", "Người Mới Đến"),
        new("Title", "title_me_truyen", "Mê Truyện"),
    };

    // Mốc (số ngày) mở khóa một khung / danh hiệu; 0 = đồ người mới; null = không tồn tại.
    // Danh hiệu được lưu theo TÊN, khung theo MÃ (giống cách lưu cũ trong UserStreaks).
    public static int? UnlockDays(string kind, string key)
    {
        bool Match(StreakRewardDef r) => r.Kind == kind && (kind == "Title" ? r.Name == key : r.Code == key);
        if (StarterItems.Any(Match)) return 0;
        return Milestones.FirstOrDefault(m => m.Rewards.Any(Match))?.Days;
    }

    // Đã mở khóa chưa: đồ người mới luôn mở; đồ theo chuỗi mở khi CHUỖI DÀI NHẤT từng đạt >= mốc
    // (hoặc đã nhận từ trước). Nhờ vậy thêm khung mới vào mốc cũ thì người đã qua mốc vẫn có ngay.
    public static bool IsUnlocked(string kind, string key, int longestStreak, IEnumerable<(string Kind, string Code, string Name)> owned)
    {
        var days = UnlockDays(kind, key);
        if (days is null) return false;
        if (days == 0 || longestStreak >= days) return true;
        return owned.Any(o => o.Kind == kind && (kind == "Title" ? o.Name == key : o.Code == key));
    }

    // Phần thưởng hiển thị trong "Bộ sưu tập": huy hiệu / danh hiệu / khung
    public static IEnumerable<(int Milestone, StreakRewardDef Def)> CollectionItems()
        => Milestones.SelectMany(m => m.Rewards
            .Where(r => r.Kind is "Badge" or "Title" or "Frame")
            .Select(r => (m.Days, r)));

}

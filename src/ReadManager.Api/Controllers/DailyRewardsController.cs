using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// ĐIỂM DANH + NHIỆM VỤ HẰNG NGÀY (trang "Khám phá" của Mobile)
//   GET  api/me/daily-rewards                       → 7 ô điểm danh tuần này + nhiệm vụ hôm nay + số xu
//   POST api/me/daily-rewards/check-in              → điểm danh hôm nay (mỗi ngày 1 lần)
//   POST api/me/daily-rewards/missions/{key}/claim  → nhận thưởng nhiệm vụ đã hoàn thành (mỗi ngày 1 lần)
//
// KHÔNG thêm bảng / migration: dùng sổ xu có sẵn (CoinTransactions). Mỗi lần nhận thưởng có SourceKey riêng
// (vd "checkin:2026-11-22", "mission:read3:2026-11-22"). CoinLedger không cho trùng SourceKey nên
// bấm nhiều lần / gửi lại yêu cầu cũng chỉ được cộng xu MỘT lần.
// "Hôm nay" tính theo giờ Việt Nam (UTC+7), giống chuỗi đọc sách.
[ApiController, Authorize]
public class DailyRewardsController(AppDbContext db) : ControllerBase
{
    private const string CheckInKind = "DailyCheckIn";
    private const string MissionKind = "DailyMission";

    // Thưởng điểm danh: Thứ 2 → Thứ 7 mỗi ngày 10 xu, Chủ nhật 30 xu
    private static int CheckInAmount(DayOfWeek day) => day == DayOfWeek.Sunday ? 30 : 10;

    // Nhiệm vụ hằng ngày (key, tên, mô tả, mục tiêu, thưởng)
    private static readonly (string Key, string Title, string Description, int Target, int Reward)[] Missions =
    {
        ("read1", "Đọc hoàn thành 1 chương", "Đọc hết một chương (tối thiểu 1 phút)", 1, 5),
        ("read3", "Đọc hoàn thành 3 chương mới", "Ba chương bạn chưa từng nhận xu", 3, 15),
        ("comment", "Bình luận truyện", "Viết 1 bình luận ở truyện hoặc chương bất kỳ", 1, 5),
    };

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static DateOnly Today => StreakService.TodayVn(DateTime.UtcNow);

    // 00:00 giờ Việt Nam của một ngày, đổi ra UTC để so với CreatedAt (lưu UTC)
    private static DateTime StartUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue).AddHours(-7);

    private static string CheckInKey(DateOnly day) => $"checkin:{day:yyyy-MM-dd}";
    private static string MissionKey(string key, DateOnly day) => $"mission:{key}:{day:yyyy-MM-dd}";

    private static readonly string[] DayLabels = { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };

    [HttpGet("api/me/daily-rewards")]
    public async Task<IActionResult> Get()
    {
        var today = Today;
        // Thứ 2 của tuần hiện tại
        var offset = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-offset);

        var keys = Enumerable.Range(0, 7).Select(i => CheckInKey(monday.AddDays(i))).ToList();
        var claimedKeys = (await db.CoinTransactions.AsNoTracking()
                .Where(t => t.UserId == UserId && keys.Contains(t.SourceKey))
                .Select(t => t.SourceKey).ToListAsync())
            .ToHashSet();

        var days = Enumerable.Range(0, 7).Select(i =>
        {
            var d = monday.AddDays(i);
            var claimed = claimedKeys.Contains(CheckInKey(d));
            var status = claimed ? "claimed" : d < today ? "missed" : d == today ? "available" : "upcoming";
            return new
            {
                date = d.ToString("yyyy-MM-dd"),
                label = d == today ? "Hôm nay" : DayLabels[i],
                amount = CheckInAmount(d.DayOfWeek),
                status
            };
        }).ToList();

        var missions = await BuildMissionsAsync(today);
        var coins = await CoinLedger.BalanceAsync(db, UserId);

        return Ok(new
        {
            today = today.ToString("yyyy-MM-dd"),
            coins,
            checkedInToday = claimedKeys.Contains(CheckInKey(today)),
            weekClaimed = claimedKeys.Count,
            days,
            missions
        });
    }

    [HttpPost("api/me/daily-rewards/check-in")]
    public async Task<IActionResult> CheckIn()
    {
        var today = Today;
        var amount = CheckInAmount(today.DayOfWeek);
        var (granted, coins) = await GrantAsync(CheckInKey(today), amount, CheckInKind,
            $"Điểm danh ngày {today:dd/MM/yyyy}");
        if (!granted) return Conflict(new { message = "Hôm nay bạn đã điểm danh rồi.", coins });
        return Ok(new { amount, coins });
    }

    [HttpPost("api/me/daily-rewards/missions/{key}/claim")]
    public async Task<IActionResult> ClaimMission(string key)
    {
        var today = Today;
        var mission = (await BuildMissionsAsync(today)).FirstOrDefault(m => m.Key == key);
        if (mission is null) return NotFound(new { message = "Nhiệm vụ không tồn tại." });
        if (!mission.Completed) return BadRequest(new { message = "Bạn chưa hoàn thành nhiệm vụ này." });

        var (granted, coins) = await GrantAsync(MissionKey(key, today), mission.Reward, MissionKind,
            $"Nhiệm vụ: {mission.Title}");
        if (!granted) return Conflict(new { message = "Bạn đã nhận thưởng nhiệm vụ này hôm nay.", coins });
        return Ok(new { amount = mission.Reward, coins });
    }

    // ---------------------------------------------------------------

    public record MissionDto(string Key, string Title, string Description, int Progress, int Target,
        int Reward, bool Completed, bool Claimed);

    private async Task<List<MissionDto>> BuildMissionsAsync(DateOnly today)
    {
        var start = StartUtc(today);
        var end = start.AddDays(1);

        // Chương đọc hoàn thành hôm nay (chương mới được cộng 10 xu)
        var newChapters = await db.CoinTransactions.AsNoTracking()
            .CountAsync(t => t.UserId == UserId && t.Kind == "ChapterReward" && t.CreatedAt >= start && t.CreatedAt < end);
        // Đã có lượt đọc hợp lệ hôm nay (kể cả đọc lại chương cũ) → tính là đã đọc ≥ 1 chương
        var readToday = await db.QualifiedReadingDays.AsNoTracking()
            .AnyAsync(d => d.UserId == UserId && d.ReadDate == today);
        var comments = await db.Comments.AsNoTracking()
            .CountAsync(c => c.UserId == UserId && c.CreatedAt >= start && c.CreatedAt < end);

        var missionKeys = Missions.Select(m => MissionKey(m.Key, today)).ToList();
        var claimed = (await db.CoinTransactions.AsNoTracking()
                .Where(t => t.UserId == UserId && missionKeys.Contains(t.SourceKey))
                .Select(t => t.SourceKey).ToListAsync())
            .ToHashSet();

        return Missions.Select(m =>
        {
            var progress = m.Key switch
            {
                "read1" => Math.Max(newChapters, readToday ? 1 : 0),
                "read3" => newChapters,
                "comment" => comments,
                _ => 0
            };
            progress = Math.Min(progress, m.Target);
            return new MissionDto(m.Key, m.Title, m.Description, progress, m.Target, m.Reward,
                progress >= m.Target, claimed.Contains(MissionKey(m.Key, today)));
        }).ToList();
    }

    // Cộng xu trong một giao dịch, khóa dòng người dùng để hai yêu cầu cùng lúc không cộng trùng.
    // Trả về (đã cộng?, số dư sau cùng).
    private async Task<(bool Granted, long Coins)> GrantAsync(string sourceKey, int amount, string kind, string description)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE UserId = {UserId} FOR UPDATE")
            .AsNoTracking().ToListAsync();
        var granted = await CoinLedger.AddAsync(db, UserId, amount, sourceKey, kind, description);
        var coins = await CoinLedger.BalanceAsync(db, UserId);
        await tx.CommitAsync();
        return (granted, coins);
    }
}

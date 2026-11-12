using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Streak;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// SPRINT 5 — CHUỖI ĐỌC SÁCH
public interface IStreakService
{
    // ChapterService gọi mỗi khi người dùng mở đọc 1 chương → cập nhật chuỗi, trao thưởng.
    Task RecordReadAsync(int userId);

    Task<StreakDto> GetAsync(int userId);
    // Khôi phục chuỗi vừa mất. Ném InvalidOperationException (→ 409) kèm lý do nếu không được.
    Task<StreakDto> RestoreAsync(int userId);
    Task<StreakDto> UpdateAppearanceAsync(int userId, UpdateAppearanceDto dto);

    Task<List<CouponDto>> GetUsableCouponsAsync(int userId);

    Task<List<StreakNoticeDto>> GetNoticesAsync(int userId, int take);
    Task MarkNoticeReadAsync(int userId, int noticeId);
    Task MarkAllNoticesReadAsync(int userId);

    Task<List<StreakLeaderboardItemDto>> GetLeaderboardAsync(int top);

    // Chạy nền: kiểm tra đứt chuỗi + gửi nhắc "sắp mất chuỗi". Trả về số thông báo đã tạo.
    Task<int> RunReminderPassAsync(DateTime? nowUtc = null);
}

public class StreakService : IStreakService
{
    private static readonly TimeZoneInfo VietnamTime = FindVietnamTimeZone();
    private const int WarnFromHourVn = 20;      // từ 20h tối mà chưa đọc → nhắc
    private const int WarnMinStreak = 3;        // chuỗi >= 3 ngày mới nhắc

    private readonly AppDbContext _db;

    public StreakService(AppDbContext db) => _db = db;

    // ================= GIỜ VIỆT NAM =================
    public static DateOnly TodayVn(DateTime utcNow)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), VietnamTime));

    public static int HourVn(DateTime utcNow)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), VietnamTime).Hour;

    // Đầu ngày (00:00 giờ VN) của 1 ngày → UTC
    private static DateTime StartOfDayUtc(DateOnly d)
        => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), VietnamTime);

    // ================= LẤY / TẠO DÒNG TRẠNG THÁI =================
    private async Task<UserStreak> GetOrCreateAsync(int userId)
    {
        var st = await _db.UserStreaks.FirstOrDefaultAsync(x => x.UserId == userId);
        if (st is not null) return st;
        st = new UserStreak { UserId = userId };
        _db.UserStreaks.Add(st);
        return st;
    }

    private void AddNotice(int userId, string kind, string message)
        => _db.StreakNotices.Add(new StreakNotice { UserId = userId, Kind = kind, Message = message });

    // ================= ĐÁNH GIÁ: có ngày nào bị bỏ lỡ không? =================
    // - Bỏ lỡ đúng 1 ngày và còn Thẻ bảo vệ → tự dùng thẻ, chuỗi giữ nguyên.
    // - Bỏ lỡ từ 2 ngày trở lên (hoặc hết thẻ) → chuỗi đứt, lưu lại để khôi phục (nếu còn lượt).
    public void Evaluate(UserStreak st, DateOnly today)
    {
        if (st.LastReadDate is null || st.CurrentStreak <= 0) return;
        var last = st.LastReadDate.Value;
        if (last >= today.AddDays(-1)) return;          // hôm nay hoặc hôm qua vẫn còn trong chuỗi

        if (last == today.AddDays(-2) && st.FreezeCards > 0)
        {
            st.FreezeCards--;
            st.LastReadDate = today.AddDays(-1);        // thẻ "đọc thay" cho ngày bị lỡ
            AddNotice(st.UserId, "FreezeUsed",
                $"Bạn lỡ mất 1 ngày đọc, Thẻ bảo vệ chuỗi đã được dùng để giữ chuỗi {st.CurrentStreak} ngày. Còn {st.FreezeCards} thẻ. Hôm nay hãy đọc để chuỗi tiếp tục nhé!");
            st.UpdatedAt = DateTime.UtcNow;
            return;
        }

        // Chuỗi đứt
        var lost = st.CurrentStreak;
        var canRestore = st.RestoresUsed < StreakCatalog.MaxRestores;
        st.BrokenStreak = canRestore ? lost : 0;
        st.BrokenAt = StartOfDayUtc(last.AddDays(2));   // thời điểm bắt đầu ngày đầu tiên bị lỡ không bù
        st.CurrentStreak = 0;
        st.UpdatedAt = DateTime.UtcNow;

        if (canRestore)
        {
            var left = StreakCatalog.MaxRestores - st.RestoresUsed;
            AddNotice(st.UserId, "Broken",
                $"Bạn đã mất chuỗi {lost} ngày. Khôi phục trong vòng 72 giờ (còn {left}/{StreakCatalog.MaxRestores} lượt khôi phục).");
        }
        else
        {
            AddNotice(st.UserId, "Broken",
                $"Bạn đã mất chuỗi {lost} ngày và đã dùng hết {StreakCatalog.MaxRestores}/{StreakCatalog.MaxRestores} lượt khôi phục. Hãy bắt đầu lại chuỗi từ hôm nay!");
        }
    }

    // ================= GHI NHẬN ĐỌC =================
    public async Task RecordReadAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var today = TodayVn(now);
        var st = await GetOrCreateAsync(userId);

        if (st.LastReadDate == today && st.CurrentStreak > 0) return;   // hôm nay đã tính rồi

        Evaluate(st, today);

        st.CurrentStreak = st.CurrentStreak <= 0 ? 1 : st.CurrentStreak + 1;
        if (st.CurrentStreak >= 2) st.EarnedStreakCoins += 5;
        st.LastReadDate = today;
        if (st.CurrentStreak > st.LongestStreak) st.LongestStreak = st.CurrentStreak;
        st.UpdatedAt = now;

        await GrantMilestonesAsync(st);

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // 2 request cùng tạo dòng trạng thái lần đầu → bỏ qua, lần đọc sau sẽ cập nhật
            _db.ChangeTracker.Clear();
        }
    }

    // ================= TRAO THƯỞNG THEO MỐC =================
    // Mỗi mốc chỉ nhận 1 lần duy nhất (kể cả khi mất chuỗi rồi cày lại).
    private async Task GrantMilestonesAsync(UserStreak st)
    {
        var claimed = await _db.StreakRewards.Where(r => r.UserId == st.UserId)
            .Select(r => r.Milestone).Distinct().ToListAsync();
        var claimedLocal = _db.StreakRewards.Local.Where(r => r.UserId == st.UserId).Select(r => r.Milestone);
        var claimedSet = claimed.Concat(claimedLocal).ToHashSet();

        foreach (var m in StreakCatalog.Milestones)
        {
            if (st.CurrentStreak < m.Days || claimedSet.Contains(m.Days)) continue;
            claimedSet.Add(m.Days);

            var lines = new List<string>();
            if (m.FreezeCards > 0)
            {
                st.FreezeCards = Math.Min(StreakCatalog.MaxFreezeCards, st.FreezeCards + m.FreezeCards);
                _db.StreakRewards.Add(new StreakReward { UserId = st.UserId, Milestone = m.Days, Kind = "Freeze", Code = "freeze", Name = $"{m.FreezeCards} Thẻ bảo vệ chuỗi" });
                lines.Add($"{m.FreezeCards} Thẻ bảo vệ chuỗi");
            }
            if (m.Coupon is not null)
            {
                _db.Coupons.Add(new Coupon
                {
                    UserId = st.UserId, Milestone = m.Days, Percent = m.Coupon.Percent,
                    DurationMonthsCsv = string.Join(",", m.Coupon.DurationMonths),
                    Name = m.Coupon.Name,
                    ExpiresAt = DateTime.UtcNow.AddDays(StreakCatalog.CouponLifetimeDays)
                });
                _db.StreakRewards.Add(new StreakReward { UserId = st.UserId, Milestone = m.Days, Kind = "Coupon", Code = $"coupon_{m.Days}", Name = m.Coupon.Name });
                lines.Add(m.Coupon.Name);
            }
            foreach (var r in m.Rewards)
            {
                _db.StreakRewards.Add(new StreakReward { UserId = st.UserId, Milestone = m.Days, Kind = r.Kind, Code = r.Code, Name = r.Name });
                lines.Add(r.Name);
                // Tự đeo món đầu tiên của loại đó nếu chưa đeo gì
                if (r.Kind == "Title" && string.IsNullOrEmpty(st.SelectedTitle)) st.SelectedTitle = r.Name;
                if (r.Kind == "Frame" && string.IsNullOrEmpty(st.SelectedFrame)) st.SelectedFrame = r.Code;
            }

            AddNotice(st.UserId, "Milestone",
                $"🎉 Chúc mừng! Bạn đạt mốc {m.Days} ngày ({m.Name}). Phần thưởng: {string.Join(", ", lines)}.");
        }
    }

    // ================= KHÔI PHỤC =================
    public async Task<StreakDto> RestoreAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var today = TodayVn(now);
        var st = await GetOrCreateAsync(userId);
        Evaluate(st, today);

        if (st.BrokenStreak <= 0 || st.BrokenAt is null)
            throw new InvalidOperationException(st.RestoresUsed >= StreakCatalog.MaxRestores
                ? $"Bạn đã dùng hết {StreakCatalog.MaxRestores} lượt khôi phục chuỗi."
                : "Hiện không có chuỗi nào để khôi phục.");
        if (st.RestoresUsed >= StreakCatalog.MaxRestores)
            throw new InvalidOperationException($"Bạn đã dùng hết {StreakCatalog.MaxRestores} lượt khôi phục chuỗi.");
        if (now > st.BrokenAt.Value + StreakCatalog.RestoreWindow)
            throw new InvalidOperationException("Đã quá 72 giờ kể từ lúc đứt chuỗi, không thể khôi phục nữa.");

        var restored = st.BrokenStreak;
        var readToday = st.LastReadDate == today && st.CurrentStreak > 0;

        // Đã đọc lại hôm nay (CurrentStreak = số ngày mới) → cộng dồn; chưa đọc → đặt lại và coi như hôm qua đã đọc
        if (readToday) st.CurrentStreak += restored;
        else { st.CurrentStreak = restored; st.LastReadDate = today.AddDays(-1); }

        if (st.CurrentStreak > st.LongestStreak) st.LongestStreak = st.CurrentStreak;
        st.BrokenStreak = 0;
        st.BrokenAt = null;
        st.RestoresUsed++;
        st.UpdatedAt = now;

        AddNotice(userId, "Restored",
            $"Đã khôi phục chuỗi! Chuỗi hiện tại: {st.CurrentStreak} ngày. Còn {StreakCatalog.MaxRestores - st.RestoresUsed}/{StreakCatalog.MaxRestores} lượt khôi phục.");
        await GrantMilestonesAsync(st);
        await _db.SaveChangesAsync();
        return await GetAsync(userId);
    }

    // ================= ĐỔI DANH HIỆU / KHUNG =================
    public async Task<StreakDto> UpdateAppearanceAsync(int userId, UpdateAppearanceDto dto)
    {
        var st = await GetOrCreateAsync(userId);
        var rewards = await _db.StreakRewards.AsNoTracking().Where(r => r.UserId == userId).ToListAsync();

        if (string.IsNullOrWhiteSpace(dto.Title)) st.SelectedTitle = null;
        else
        {
            if (!rewards.Any(r => r.Kind == "Title" && r.Name == dto.Title))
                throw new ArgumentException("Bạn chưa mở khóa danh hiệu này.");
            st.SelectedTitle = dto.Title;
        }

        if (string.IsNullOrWhiteSpace(dto.Frame)) st.SelectedFrame = null;
        else
        {
            if (!rewards.Any(r => r.Kind == "Frame" && r.Code == dto.Frame))
                throw new ArgumentException("Bạn chưa mở khóa khung avatar này.");
            st.SelectedFrame = dto.Frame;
        }

        st.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetAsync(userId);
    }

    // ================= XEM TRẠNG THÁI =================
    public async Task<StreakDto> GetAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var today = TodayVn(now);
        var st = await GetOrCreateAsync(userId);
        Evaluate(st, today);                 // phát hiện đứt chuỗi ngay khi người dùng mở màn hình
        if (_db.ChangeTracker.HasChanges())
        {
            try { await _db.SaveChangesAsync(); } catch (DbUpdateException) { _db.ChangeTracker.Clear(); st = await GetOrCreateAsync(userId); }
        }

        var rewards = await _db.StreakRewards.AsNoTracking().Where(r => r.UserId == userId).ToListAsync();
        var has = rewards.Select(r => r.Kind + ":" + r.Code).ToHashSet();
        var reached = rewards.Select(r => r.Milestone).ToHashSet();

        // 7 ngày gần nhất
        var from = today.AddDays(-6);
        var fromUtc = StartOfDayUtc(from);
        var readAts = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.UserId == userId && r.ReadAt >= fromUtc)
            .Select(r => r.ReadAt).ToListAsync();
        var readDays = readAts.Select(TodayVn).ToHashSet();
        var days = new List<StreakDayDto>();
        for (var i = 0; i < 7; i++)
        {
            var d = from.AddDays(i);
            days.Add(new StreakDayDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                Label = d.DayOfWeek switch
                {
                    DayOfWeek.Monday => "T2", DayOfWeek.Tuesday => "T3", DayOfWeek.Wednesday => "T4",
                    DayOfWeek.Thursday => "T5", DayOfWeek.Friday => "T6", DayOfWeek.Saturday => "T7", _ => "CN"
                },
                Read = readDays.Contains(d),
                IsToday = d == today
            });
        }

        var next = StreakCatalog.Milestones.FirstOrDefault(m => !reached.Contains(m.Days));

        var canRestore = st.BrokenStreak > 0 && st.BrokenAt is not null
                         && st.RestoresUsed < StreakCatalog.MaxRestores
                         && now <= st.BrokenAt.Value + StreakCatalog.RestoreWindow;

        return new StreakDto
        {
            CurrentStreak = st.CurrentStreak,
            LongestStreak = st.LongestStreak,
            ReadToday = st.LastReadDate == today && st.CurrentStreak > 0,
            FreezeCards = st.FreezeCards,
            RestoresUsed = st.RestoresUsed,
            RestoresLeft = Math.Max(0, StreakCatalog.MaxRestores - st.RestoresUsed),
            CanRestore = canRestore,
            BrokenStreak = canRestore ? st.BrokenStreak : 0,
            RestoreDeadline = canRestore ? st.BrokenAt!.Value + StreakCatalog.RestoreWindow : null,
            IconTier = has.Contains("Perk:perk_icon_do") ? "darkred" : "orange",
            SelectedTitle = st.SelectedTitle,
            SelectedFrame = st.SelectedFrame,
            NextMilestoneDays = next?.Days ?? 0,
            NextMilestoneName = next?.Name,
            UnreadNotices = await _db.StreakNotices.CountAsync(n => n.UserId == userId && !n.IsRead),
            Last7Days = days,
            Milestones = StreakCatalog.Milestones.Select(m => new StreakMilestoneDto
            {
                Days = m.Days,
                Name = m.Name,
                Reached = reached.Contains(m.Days),
                Rewards = DescribeRewards(m, reached.Contains(m.Days))
            }).ToList(),
            Collection = StreakCatalog.CollectionItems().Select(x => new StreakRewardItemDto
            {
                Kind = x.Def.Kind, Code = x.Def.Code, Name = x.Def.Name,
                Unlocked = has.Contains(x.Def.Kind + ":" + x.Def.Code)
            }).ToList()
        };
    }

    private static List<StreakRewardItemDto> DescribeRewards(StreakMilestoneDef m, bool unlocked)
    {
        var list = new List<StreakRewardItemDto>();
        if (m.FreezeCards > 0)
            list.Add(new() { Kind = "Freeze", Code = "freeze", Name = $"{m.FreezeCards} Thẻ bảo vệ chuỗi", Unlocked = unlocked });
        if (m.Coupon is not null)
            list.Add(new() { Kind = "Coupon", Code = $"coupon_{m.Days}", Name = m.Coupon.Name, Unlocked = unlocked });
        foreach (var r in m.Rewards)
            list.Add(new() { Kind = r.Kind, Code = r.Code, Name = r.Name, Unlocked = unlocked });
        return list;
    }

    // ================= COUPON =================
    // Coupon dùng được = chưa hết hạn và KHÔNG có đơn Pending/Paid nào đang dùng nó.
    public async Task<List<CouponDto>> GetUsableCouponsAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var list = await _db.Coupons.AsNoTracking()
            .Where(c => c.UserId == userId && c.ExpiresAt > now)
            .Where(c => !_db.Orders.Any(o => o.CouponId == c.CouponId && (o.Status == "Pending" || o.Status == "Paid")))
            .OrderByDescending(c => c.Percent)
            .ToListAsync();
        return list.Select(c => new CouponDto
        {
            CouponId = c.CouponId,
            Name = c.Name,
            Percent = c.Percent,
            DurationMonths = c.DurationMonthsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList(),
            ExpiresAt = c.ExpiresAt
        }).ToList();
    }

    // ================= THÔNG BÁO =================
    public async Task<List<StreakNoticeDto>> GetNoticesAsync(int userId, int take)
        => await _db.StreakNotices.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.StreakNoticeId)
            .Take(Math.Clamp(take, 1, 100))
            .Select(n => new StreakNoticeDto
            {
                StreakNoticeId = n.StreakNoticeId, Kind = n.Kind, Message = n.Message,
                IsRead = n.IsRead, CreatedAt = n.CreatedAt
            }).ToListAsync();

    public async Task MarkNoticeReadAsync(int userId, int noticeId)
        => await _db.StreakNotices.Where(n => n.UserId == userId && n.StreakNoticeId == noticeId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    public async Task MarkAllNoticesReadAsync(int userId)
        => await _db.StreakNotices.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    // ================= BẢNG XẾP HẠNG CHUỖI =================
    public async Task<List<StreakLeaderboardItemDto>> GetLeaderboardAsync(int top)
    {
        top = Math.Clamp(top, 1, 100);
        var rows = await _db.UserStreaks.AsNoTracking()
            .Where(s => s.LongestStreak > 0 && s.User.AccountStatus != AccountStatuses.Disabled)
            .OrderByDescending(s => s.LongestStreak).ThenByDescending(s => s.CurrentStreak)
            .Take(top)
            .Select(s => new
            {
                s.UserId, s.User.DisplayName, s.User.AvatarUrl,
                s.SelectedTitle, s.SelectedFrame, s.CurrentStreak, s.LongestStreak,
                Medal = _db.StreakRewards.Any(r => r.UserId == s.UserId && r.Code == "perk_medal")
            }).ToListAsync();

        return rows.Select((r, i) => new StreakLeaderboardItemDto
        {
            Rank = i + 1, UserId = r.UserId, DisplayName = r.DisplayName, AvatarUrl = r.AvatarUrl,
            Title = r.SelectedTitle, Frame = r.SelectedFrame,
            CurrentStreak = r.CurrentStreak, LongestStreak = r.LongestStreak, Medal = r.Medal
        }).ToList();
    }

    // ================= CHẠY NỀN: NHẮC NHỞ =================
    public async Task<int> RunReminderPassAsync(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var today = TodayVn(now);
        var created = 0;

        // Chỉ xét người đang có chuỗi (đọc trong 3 ngày gần đây)
        var minDate = today.AddDays(-3);
        var streaks = await _db.UserStreaks
            .Where(s => s.CurrentStreak > 0 && s.LastReadDate != null && s.LastReadDate >= minDate)
            .Take(2000)
            .ToListAsync();

        foreach (var st in streaks)
        {
            var before = _db.StreakNotices.Local.Count;
            Evaluate(st, today);                      // đứt chuỗi → tạo thông báo "Broken"

            // Sắp mất chuỗi: chưa đọc hôm nay, đã tối, chuỗi đủ dài, hôm nay chưa nhắc
            if (st.CurrentStreak >= WarnMinStreak
                && st.LastReadDate == today.AddDays(-1)
                && HourVn(now) >= WarnFromHourVn
                && st.LastWarnDate != today)
            {
                st.LastWarnDate = today;
                var cover = st.FreezeCards > 0 ? $" (Bạn còn {st.FreezeCards} Thẻ bảo vệ sẽ tự dùng nếu lỡ 1 ngày.)" : "";
                AddNotice(st.UserId, "AtRisk",
                    $"🔥 Chuỗi {st.CurrentStreak} ngày của bạn sắp mất! Hãy đọc ít nhất 1 chương trước 24h hôm nay.{cover}");
            }
            created += _db.StreakNotices.Local.Count - before;
        }

        if (_db.ChangeTracker.HasChanges()) await _db.SaveChangesAsync();
        return created;
    }

    private static TimeZoneInfo FindVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh", "Asia/Bangkok" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}

// Chạy mỗi 15 phút để nhắc nhở / phát hiện đứt chuỗi cho cả người không mở app.
public class StreakReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<StreakReminderWorker> _log;

    public StreakReminderWorker(IServiceScopeFactory scopes, ILogger<StreakReminderWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IStreakService>();
                var n = await svc.RunReminderPassAsync();
                if (n > 0) _log.LogInformation("Streak reminders: {Count} thông báo mới", n);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Streak reminder pass failed");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

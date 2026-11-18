using System.Text;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Statistics.Activity;

namespace ReadManager.Api.Services;

// BACKEND 2 — PB37: THỐNG KÊ HOẠT ĐỘNG (Bản nâng cấp thực tế chuẩn các nền tảng đọc truyện thương mại)
// Bao gồm:
//   1. Thống kê thành viên, đăng ký mới, gia hạn, mua truyện, trạng thái giao dịch & tăng trưởng so với kỳ trước (% Growth)
//   2. Giữ chân thuê bao (Retention, Churn, Cảnh báo gói sắp hết hạn trong 7 ngày)
//   3. Biểu đồ theo từng ngày (kèm DAU — Daily Active Readers & số chương đọc)
//   4. Phân tích 24 Khung giờ vàng đọc truyện & mua sắm (Peak Hours 0h–23h giờ VN)
//   5. Bảng xếp hạng Top Độc Giả tích cực & Top VIP chi tiêu (Top Readers & Supporters)
//   6. Phễu chuyển đổi người dùng 5 bước (Conversion Funnel)
//   7. Dòng sự kiện hoạt động trực tiếp (Recent Activity Feed) & Xuất báo cáo Excel/CSV
public interface IActivityStatisticsService
{
    Task<ActivityOverviewDto> GetOverviewAsync(ActivityStatsQuery query);
    Task<List<DailyActivityStatsDto>> GetDailyAsync(ActivityStatsQuery query);
    Task<TransactionStatusSummaryDto> GetTransactionsAsync(ActivityStatsQuery query);
    Task<List<PlanActivityStatsDto>> GetPlansAsync(ActivityStatsQuery query);
    Task<MemberActivitySummaryDto> GetMembersAsync(ActivityStatsQuery query);

    // ----- Các tính năng mở rộng thực tế -----
    Task<List<HourlyActivityStatsDto>> GetPeakHoursAsync(ActivityStatsQuery query);
    Task<List<TopReaderActivityDto>> GetTopReadersAsync(ActivityStatsQuery query);
    Task<ActivityFunnelDto> GetFunnelAsync(ActivityStatsQuery query);
    Task<List<RecentActivityEventDto>> GetRecentFeedAsync(int top = 20);
    Task<byte[]> ExportDailyCsvAsync(ActivityStatsQuery query);
}

public class ActivityStatisticsService : IActivityStatisticsService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;
    private const int ExpiringSoonWindowDays = 7;

    private const string RoleMember = "Member";
    private const string RoleAdmin = "Admin";
    private const string AccountActive = "Active";
    private const string CommentVisible = "Visible";

    private const string TypePlan = "Plan";
    private const string TypeStory = "Story";

    private const string StatusPaid = "Paid";
    private const string StatusPending = "Pending";
    private const string StatusFailed = "Failed";
    private const string StatusCancelled = "Cancelled";
    private const string StatusExpired = "Expired";

    private static readonly string[] AllOrderStatuses =
    {
        StatusPaid, StatusPending, StatusFailed, StatusCancelled, StatusExpired
    };

    private static readonly TimeZoneInfo VietnamTime = FindVietnamTimeZone();

    private readonly AppDbContext _db;

    public ActivityStatisticsService(AppDbContext db)
    {
        _db = db;
    }

    // =====================================================================
    // 1. TỔNG QUAN HOẠT ĐỘNG & CHỈ SỐ TĂNG TRƯỞNG (PB37)
    // =====================================================================
    public async Task<ActivityOverviewDto> GetOverviewAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);
        var prevRange = PreviousRange(range);

        var memberStats = await BuildMemberSummaryAsync(range);
        var planEvents = await LoadClassifiedPlanOrdersAsync();

        var planEventsInRange = planEvents
            .Where(e => e.EffectiveAtUtc >= range.FromUtc && e.EffectiveAtUtc < range.ToUtc)
            .ToList();
        var planEventsPrev = planEvents
            .Where(e => e.EffectiveAtUtc >= prevRange.FromUtc && e.EffectiveAtUtc < prevRange.ToUtc)
            .ToList();

        var newSubscriptions = planEventsInRange.Count(e => !e.IsRenewal);
        var renewals = planEventsInRange.Count(e => e.IsRenewal);
        var totalPlanOrdersPaid = newSubscriptions + renewals;

        var storyOrdersPaid = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderType == TypeStory && o.Status == StatusPaid)
            .Select(o => new { o.UserId, EffectiveAt = o.PaidAt ?? o.CreatedAt })
            .ToListAsync();

        var storyPurchasesInRange = storyOrdersPaid
            .Where(o => o.EffectiveAt >= range.FromUtc && o.EffectiveAt < range.ToUtc)
            .ToList();
        var storyPurchasesPrevCount = storyOrdersPaid
            .Count(o => o.EffectiveAt >= prevRange.FromUtc && o.EffectiveAt < prevRange.ToUtc);

        var uniqueBuyers = planEventsInRange.Select(e => e.UserId)
            .Concat(storyPurchasesInRange.Select(s => s.UserId))
            .Distinct()
            .Count();

        // So sánh kỳ trước để tính % tăng trưởng
        var prevNewMembers = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == RoleMember && u.CreatedAt >= prevRange.FromUtc && u.CreatedAt < prevRange.ToUtc);

        var prevActiveReaders = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.UserId != null && r.ReadAt >= prevRange.FromUtc && r.ReadAt < prevRange.ToUtc)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .CountAsync();

        // Tìm khung giờ vàng đọc truyện
        var hourly = await GetPeakHoursAsync(query);
        var peakHour = hourly
            .Where(h => h.Reads > 0)
            .OrderByDescending(h => h.Reads)
            .Select(h => (int?)h.Hour)
            .FirstOrDefault();

        var txSummary = await BuildTransactionSummaryAsync(range);
        var plans = await BuildPlanStatsAsync(range, planEventsInRange);
        var topReaders = await GetTopReadersAsync(new ActivityStatsQuery
        {
            From = range.From,
            To = range.To,
            Sort = "score",
            Top = 5
        });
        var recentActivities = await GetRecentFeedAsync(10);

        var renewalRate = totalPlanOrdersPaid > 0
            ? Math.Round(renewals * 100.0 / totalPlanOrdersPaid, 2)
            : 0;

        var conversionRate = memberStats.ActiveReadersInPeriod > 0
            ? Math.Round(Math.Min(100.0, uniqueBuyers * 100.0 / memberStats.ActiveReadersInPeriod), 2)
            : 0;

        return new ActivityOverviewDto
        {
            From = range.From,
            To = range.To,
            TotalMembers = memberStats.TotalMembers,
            ActiveAccounts = memberStats.ActiveAccounts,
            LockedAccounts = memberStats.LockedAccounts,
            NewMembers = memberStats.NewRegistrations,
            ActiveReaders = memberStats.ActiveReadersInPeriod,
            ActiveSubscribers = memberStats.ActiveSubscribersNow,
            ExpiringSoonSubscriptions = memberStats.ExpiringSoonSubscribers,
            ChurnedSubscriptions = memberStats.ChurnedSubscribers,
            NewSubscriptions = newSubscriptions,
            Renewals = renewals,
            TotalPlanOrdersPaid = totalPlanOrdersPaid,
            StoryPurchases = storyPurchasesInRange.Count,
            UniqueBuyers = uniqueBuyers,
            NewMembersGrowthPercent = GrowthPercent(memberStats.NewRegistrations, prevNewMembers),
            ActiveReadersGrowthPercent = GrowthPercent(memberStats.ActiveReadersInPeriod, prevActiveReaders),
            SubscriptionsGrowthPercent = GrowthPercent(totalPlanOrdersPaid, planEventsPrev.Count),
            StoryPurchasesGrowthPercent = GrowthPercent(storyPurchasesInRange.Count, storyPurchasesPrevCount),
            RenewalRate = renewalRate,
            ConversionRate = conversionRate,
            AverageChaptersPerReader = memberStats.AverageChaptersPerReader,
            PeakReadingHour = peakHour,
            TotalOrders = txSummary.TotalOrders,
            PaidOrders = txSummary.PaidCount,
            PendingOrders = txSummary.PendingCount,
            FailedOrders = txSummary.FailedCount,
            CancelledOrders = txSummary.CancelledCount,
            ExpiredOrders = txSummary.ExpiredCount,
            SuccessRate = txSummary.SuccessRate,
            TransactionsByStatus = txSummary.StatusBreakdown,
            Plans = plans,
            TopReaders = topReaders,
            RecentActivities = recentActivities
        };
    }

    // =====================================================================
    // 2. SỐ LIỆU TỪNG NGÀY (vẽ biểu đồ đường/cột + DAU)
    // =====================================================================
    public async Task<List<DailyActivityStatsDto>> GetDailyAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);

        var newMemberTimes = await _db.Users.AsNoTracking()
            .Where(u => u.Role == RoleMember && u.CreatedAt >= range.FromUtc && u.CreatedAt < range.ToUtc)
            .Select(u => u.CreatedAt)
            .ToListAsync();

        var readsInRange = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .Select(r => new { r.UserId, r.ReadAt })
            .ToListAsync();

        var planEvents = (await LoadClassifiedPlanOrdersAsync())
            .Where(e => e.EffectiveAtUtc >= range.FromUtc && e.EffectiveAtUtc < range.ToUtc)
            .ToList();

        var newSubTimes = planEvents.Where(e => !e.IsRenewal).Select(e => e.EffectiveAtUtc);
        var renewalTimes = planEvents.Where(e => e.IsRenewal).Select(e => e.EffectiveAtUtc);

        var ordersInRange = await _db.Orders.AsNoTracking()
            .Select(o => new
            {
                o.OrderType,
                o.Status,
                EffectiveAt = o.Status == StatusPaid && o.PaidAt != null ? o.PaidAt.Value : o.CreatedAt
            })
            .Where(o => o.EffectiveAt >= range.FromUtc && o.EffectiveAt < range.ToUtc)
            .ToListAsync();

        var storyPurchaseTimes = ordersInRange
            .Where(o => o.OrderType == TypeStory && o.Status == StatusPaid)
            .Select(o => o.EffectiveAt);

        var totalOrderTimes = ordersInRange.Select(o => o.EffectiveAt);
        var paidOrderTimes = ordersInRange.Where(o => o.Status == StatusPaid).Select(o => o.EffectiveAt);
        var pendingOrderTimes = ordersInRange.Where(o => o.Status == StatusPending).Select(o => o.EffectiveAt);
        var failedOrderTimes = ordersInRange
            .Where(o => o.Status == StatusFailed || o.Status == StatusCancelled || o.Status == StatusExpired)
            .Select(o => o.EffectiveAt);

        var newMembersByDay = CountByDay(newMemberTimes);
        var chaptersReadByDay = CountByDay(readsInRange.Select(r => r.ReadAt));
        var activeReadersByDay = readsInRange
            .Where(r => r.UserId.HasValue)
            .GroupBy(r => ToVnDate(r.ReadAt))
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId!.Value).Distinct().Count());

        var newSubsByDay = CountByDay(newSubTimes);
        var renewalsByDay = CountByDay(renewalTimes);
        var storyPurchasesByDay = CountByDay(storyPurchaseTimes);
        var totalOrdersByDay = CountByDay(totalOrderTimes);
        var paidOrdersByDay = CountByDay(paidOrderTimes);
        var pendingOrdersByDay = CountByDay(pendingOrderTimes);
        var failedOrdersByDay = CountByDay(failedOrderTimes);

        var result = new List<DailyActivityStatsDto>();
        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            result.Add(new DailyActivityStatsDto
            {
                Date = day,
                NewMembers = newMembersByDay.GetValueOrDefault(day),
                ActiveReaders = activeReadersByDay.GetValueOrDefault(day),
                ChaptersRead = chaptersReadByDay.GetValueOrDefault(day),
                NewSubscriptions = newSubsByDay.GetValueOrDefault(day),
                Renewals = renewalsByDay.GetValueOrDefault(day),
                StoryPurchases = storyPurchasesByDay.GetValueOrDefault(day),
                TotalOrders = totalOrdersByDay.GetValueOrDefault(day),
                PaidOrders = paidOrdersByDay.GetValueOrDefault(day),
                PendingOrders = pendingOrdersByDay.GetValueOrDefault(day),
                FailedOrders = failedOrdersByDay.GetValueOrDefault(day)
            });
        }

        return result;
    }

    // =====================================================================
    // 3. CHI TIẾT TRẠNG THÁI GIAO DỊCH
    // =====================================================================
    public async Task<TransactionStatusSummaryDto> GetTransactionsAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);
        return await BuildTransactionSummaryAsync(range);
    }

    // =====================================================================
    // 4. THỐNG KÊ THEO GÓI ĐỌC
    // =====================================================================
    public async Task<List<PlanActivityStatsDto>> GetPlansAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);
        var planEventsInRange = (await LoadClassifiedPlanOrdersAsync())
            .Where(e => e.EffectiveAtUtc >= range.FromUtc && e.EffectiveAtUtc < range.ToUtc)
            .ToList();

        return await BuildPlanStatsAsync(range, planEventsInRange);
    }

    // =====================================================================
    // 5. THỐNG KÊ CHI TIẾT THÀNH VIÊN & TỶ LỆ GIỮ CHÂN
    // =====================================================================
    public async Task<MemberActivitySummaryDto> GetMembersAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);
        return await BuildMemberSummaryAsync(range);
    }

    // =====================================================================
    // 6. PHÂN TÍCH 24 KHUNG GIỜ VÀNG ĐỌC TRUYỆN (0h..23h GIỜ VIỆT NAM)
    // =====================================================================
    public async Task<List<HourlyActivityStatsDto>> GetPeakHoursAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);

        var reads = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .Select(r => new { r.UserId, r.ReadAt })
            .ToListAsync();

        var comments = await _db.Comments.AsNoTracking()
            .Where(c => c.Status == CommentVisible && c.CreatedAt >= range.FromUtc && c.CreatedAt < range.ToUtc)
            .Select(c => c.CreatedAt)
            .ToListAsync();

        var paidOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == StatusPaid)
            .Select(o => o.PaidAt ?? o.CreatedAt)
            .Where(t => t >= range.FromUtc && t < range.ToUtc)
            .ToListAsync();

        var readsByHour = reads.ToLookup(r => ToVnHour(r.ReadAt));
        var commentsByHour = comments.ToLookup(ToVnHour);
        var ordersByHour = paidOrders.ToLookup(ToVnHour);

        var list = Enumerable.Range(0, 24).Select(hour =>
        {
            var hourReads = readsByHour[hour].ToList();
            return new HourlyActivityStatsDto
            {
                Hour = hour,
                Label = $"{hour:D2}:00 - {hour:D2}:59",
                Reads = hourReads.Count,
                UniqueReaders = hourReads.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().Count(),
                Comments = commentsByHour[hour].Count(),
                PaidOrders = ordersByHour[hour].Count()
            };
        }).ToList();

        // Đánh dấu tối đa 3 khung giờ có lượt đọc cao nhất (> 0) là Giờ cao điểm (Peak Hour)
        var peakHours = list
            .Where(x => x.Reads > 0)
            .OrderByDescending(x => x.Reads)
            .ThenByDescending(x => x.UniqueReaders)
            .Take(3)
            .Select(x => x.Hour)
            .ToHashSet();

        foreach (var item in list)
            item.IsPeakHour = peakHours.Contains(item.Hour);

        return list;
    }

    // =====================================================================
    // 7. BẢNG XẾP HẠNG TOP ĐỘC GIẢ TÍCH CỰC & VIP ỦNG HỘ
    // =====================================================================
    public async Task<List<TopReaderActivityDto>> GetTopReadersAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);
        var top = Math.Clamp(query.Top, 1, 100);
        var now = DateTime.UtcNow;

        var readsByUser = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.UserId != null && r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .GroupBy(r => r.UserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                ChaptersRead = g.Count(),
                StoriesRead = g.Select(x => x.StoryId).Distinct().Count(),
                LastReadAt = g.Max(x => (DateTime?)x.ReadAt)
            })
            .ToDictionaryAsync(x => x.UserId);

        var commentsByUser = await _db.Comments.AsNoTracking()
            .Where(c => c.Status == CommentVisible && c.CreatedAt >= range.FromUtc && c.CreatedAt < range.ToUtc)
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), LastAt = g.Max(x => (DateTime?)x.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId);

        var followsByUser = await _db.StoryFollows.AsNoTracking()
            .Where(f => f.CreatedAt >= range.FromUtc && f.CreatedAt < range.ToUtc)
            .GroupBy(f => f.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), LastAt = g.Max(x => (DateTime?)x.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId);

        var ordersByUser = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == StatusPaid)
            .Select(o => new { o.UserId, o.Amount, EffectiveAt = o.PaidAt ?? o.CreatedAt })
            .Where(o => o.EffectiveAt >= range.FromUtc && o.EffectiveAt < range.ToUtc)
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                TotalSpent = g.Sum(x => x.Amount),
                LastAt = g.Max(x => (DateTime?)x.EffectiveAt)
            })
            .ToDictionaryAsync(x => x.UserId);

        var activeSubUsers = await GetActiveSubscriberUserIdsAsync(now);

        var members = await _db.Users.AsNoTracking()
            .Where(u => u.Role == RoleMember)
            .Select(u => new { u.UserId, u.DisplayName, u.Email, u.AvatarUrl })
            .ToListAsync();

        var items = members.Select(m =>
        {
            var r = readsByUser.GetValueOrDefault(m.UserId);
            var c = commentsByUser.GetValueOrDefault(m.UserId);
            var f = followsByUser.GetValueOrDefault(m.UserId);
            var o = ordersByUser.GetValueOrDefault(m.UserId);

            var chaptersRead = r?.ChaptersRead ?? 0;
            var storiesRead = r?.StoriesRead ?? 0;
            var comments = c?.Count ?? 0;
            var follows = f?.Count ?? 0;
            var paidOrders = o?.Count ?? 0;
            var totalSpent = o?.TotalSpent ?? 0m;
            var hasSub = activeSubUsers.Contains(m.UserId);

            var score = chaptersRead * 1 + comments * 3 + follows * 5 + paidOrders * 20;

            var lastActive = new[] { r?.LastReadAt, c?.LastAt, f?.LastAt, o?.LastAt }
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .DefaultIfEmpty()
                .Max();

            var tier = hasSub
                ? "VIP Thuê Bao"
                : paidOrders > 0
                    ? "Người Mua Truyện"
                    : chaptersRead >= 10 || score >= 20
                        ? "Độc Giả Tích Cực"
                        : "Thành Viên Mới";

            return new TopReaderActivityDto
            {
                UserId = m.UserId,
                DisplayName = m.DisplayName,
                Email = m.Email,
                AvatarUrl = m.AvatarUrl,
                MemberTier = tier,
                HasActiveSubscription = hasSub,
                ChaptersRead = chaptersRead,
                StoriesRead = storiesRead,
                CommentsPosted = comments,
                StoriesFollowed = follows,
                PaidOrdersCount = paidOrders,
                TotalSpent = totalSpent,
                ActivityScore = score,
                LastActiveAt = lastActive == default ? null : lastActive
            };
        })
        .Where(x => x.ActivityScore > 0 || x.TotalSpent > 0)
        .ToList();

        var sort = (query.Sort ?? "score").ToLowerInvariant();
        IOrderedEnumerable<TopReaderActivityDto> sorted = sort switch
        {
            "reads" => items.OrderByDescending(x => x.ChaptersRead).ThenByDescending(x => x.ActivityScore),
            "spent" => items.OrderByDescending(x => x.TotalSpent).ThenByDescending(x => x.PaidOrdersCount),
            "comments" => items.OrderByDescending(x => x.CommentsPosted).ThenByDescending(x => x.ActivityScore),
            "score" => items.OrderByDescending(x => x.ActivityScore).ThenByDescending(x => x.TotalSpent),
            _ => throw new ArgumentException("sort phải là score, reads, spent hoặc comments.")
        };

        var result = sorted.ThenBy(x => x.DisplayName).Take(top).ToList();
        for (int i = 0; i < result.Count; i++)
            result[i].Rank = i + 1;

        return result;
    }

    // =====================================================================
    // 8. PHỄU CHUYỂN ĐỔI NGƯỜI DÙNG (CONVERSION FUNNEL)
    // =====================================================================
    public async Task<ActivityFunnelDto> GetFunnelAsync(ActivityStatsQuery query)
    {
        var range = ResolveRange(query);

        var totalActiveMembers = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == RoleMember && u.AccountStatus == AccountActive);

        var readerIds = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.UserId != null && r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToListAsync();

        var commentUserIds = await _db.Comments.AsNoTracking()
            .Where(c => c.Status == CommentVisible && c.CreatedAt >= range.FromUtc && c.CreatedAt < range.ToUtc)
            .Select(c => c.UserId)
            .Distinct()
            .ToListAsync();

        var followUserIds = await _db.StoryFollows.AsNoTracking()
            .Where(f => f.CreatedAt >= range.FromUtc && f.CreatedAt < range.ToUtc)
            .Select(f => f.UserId)
            .Distinct()
            .ToListAsync();

        var engagedCount = commentUserIds.Concat(followUserIds).Distinct().Count();

        var ordersInPeriod = await _db.Orders.AsNoTracking()
            .Select(o => new
            {
                o.UserId,
                o.Status,
                EffectiveAt = o.Status == StatusPaid && o.PaidAt != null ? o.PaidAt.Value : o.CreatedAt
            })
            .Where(o => o.EffectiveAt >= range.FromUtc && o.EffectiveAt < range.ToUtc)
            .ToListAsync();

        var checkoutUsersCount = ordersInPeriod.Select(o => o.UserId).Distinct().Count();
        var payingUsersCount = ordersInPeriod.Where(o => o.Status == StatusPaid).Select(o => o.UserId).Distinct().Count();

        var topBase = Math.Max(1, totalActiveMembers);
        var rawStages = new (string Code, string Name, int Count)[]
        {
            ("Members", "Thành viên đang hoạt động", totalActiveMembers),
            ("Readers", "Độc giả có đọc chương", readerIds.Count),
            ("Engaged", "Độc giả tương tác (Theo dõi / Bình luận)", engagedCount),
            ("Checkout", "Độc giả tạo đơn mua gói / truyện", checkoutUsersCount),
            ("Paid", "Độc giả thanh toán thành công", payingUsersCount)
        };

        var stages = new List<FunnelStageDto>();
        for (int i = 0; i < rawStages.Length; i++)
        {
            var prevCount = i == 0 ? rawStages[i].Count : rawStages[i - 1].Count;
            stages.Add(new FunnelStageDto
            {
                StageCode = rawStages[i].Code,
                StageName = rawStages[i].Name,
                UserCount = rawStages[i].Count,
                PercentageOfTotal = totalActiveMembers > 0
                    ? Math.Round(rawStages[i].Count * 100.0 / topBase, 2)
                    : 0,
                ConversionFromPrevious = i == 0
                    ? 100.0
                    : prevCount > 0
                        ? Math.Round(Math.Min(100.0, rawStages[i].Count * 100.0 / prevCount), 2)
                        : 0
            });
        }

        return new ActivityFunnelDto
        {
            From = range.From,
            To = range.To,
            TotalActiveMembers = totalActiveMembers,
            ReadersCount = readerIds.Count,
            EngagedUsersCount = engagedCount,
            CheckoutUsersCount = checkoutUsersCount,
            PayingUsersCount = payingUsersCount,
            OverallConversionRate = readerIds.Count > 0
                ? Math.Round(Math.Min(100.0, payingUsersCount * 100.0 / readerIds.Count), 2)
                : 0,
            Stages = stages
        };
    }

    // =====================================================================
    // 9. DÒNG SỰ KIỆN HOẠT ĐỘNG GẦN ĐÂY (LIVE ACTIVITY FEED)
    // =====================================================================
    public async Task<List<RecentActivityEventDto>> GetRecentFeedAsync(int top = 20)
    {
        top = Math.Clamp(top, 1, 100);

        var recentUsers = await _db.Users.AsNoTracking()
            .Where(u => u.Role == RoleMember)
            .OrderByDescending(u => u.CreatedAt)
            .Take(top)
            .Select(u => new RecentActivityEventDto
            {
                EventType = "NewMember",
                Title = "Thành viên mới đăng ký",
                Description = $"{u.DisplayName} ({u.Email}) vừa tạo tài khoản.",
                UserId = u.UserId,
                UserName = u.DisplayName,
                Status = u.AccountStatus,
                OccurredAtUtc = u.CreatedAt
            })
            .ToListAsync();

        var planClassifications = (await LoadClassifiedPlanOrdersAsync())
            .ToDictionary(x => x.OrderId, x => x.IsRenewal);

        var recentOrdersRaw = await _db.Orders.AsNoTracking()
            .OrderByDescending(o => o.UpdatedAt)
            .Take(top)
            .Select(o => new
            {
                o.OrderId,
                o.UserId,
                UserName = o.User.DisplayName,
                o.OrderType,
                o.ItemName,
                o.Amount,
                o.Status,
                OccurredAt = o.Status == StatusPaid && o.PaidAt != null ? o.PaidAt.Value : o.CreatedAt
            })
            .ToListAsync();

        var recentOrderEvents = recentOrdersRaw.Select(o =>
        {
            var isRenewal = o.OrderType == TypePlan && planClassifications.GetValueOrDefault(o.OrderId);
            var (eventType, title) = o.Status switch
            {
                StatusPaid when o.OrderType == TypePlan && isRenewal => ("Renewal", $"Gia hạn gói đọc: {o.ItemName}"),
                StatusPaid when o.OrderType == TypePlan => ("NewSubscription", $"Đăng ký gói mới: {o.ItemName}"),
                StatusPaid => ("StoryPurchase", $"Mua lẻ truyện: {o.ItemName}"),
                StatusPending => ("OrderPending", $"Đơn hàng đang chờ thanh toán: {o.ItemName}"),
                _ => ("OrderFailed", $"Giao dịch {o.Status}: {o.ItemName}")
            };

            return new RecentActivityEventDto
            {
                EventType = eventType,
                Title = title,
                Description = $"{o.UserName} — {o.ItemName} ({o.Amount:N0} đ)",
                UserId = o.UserId,
                UserName = o.UserName,
                Amount = o.Amount,
                Status = o.Status,
                OccurredAtUtc = o.OccurredAt
            };
        }).ToList();

        return recentUsers
            .Concat(recentOrderEvents)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(top)
            .ToList();
    }

    // =====================================================================
    // 10. XUẤT BÁO CÁO EXCEL / CSV (UTF-8 BOM TIẾNG VIỆT)
    // =====================================================================
    public async Task<byte[]> ExportDailyCsvAsync(ActivityStatsQuery query)
    {
        var overview = await GetOverviewAsync(query);
        var daily = await GetDailyAsync(query);

        var sb = new StringBuilder();
        sb.AppendLine($"BÁO CÁO THỐNG KÊ HOẠT ĐỘNG (PB37),Từ ngày {overview.From:yyyy-MM-dd},Đến ngày {overview.To:yyyy-MM-dd}");
        sb.AppendLine($"Tổng thành viên,{overview.TotalMembers},Đăng ký mới trong kỳ,{overview.NewMembers} ({overview.NewMembersGrowthPercent:+0.##;-0.##;0}%)");
        sb.AppendLine($"Độc giả hoạt động,{overview.ActiveReaders},Thuê bao còn hạn,{overview.ActiveSubscribers},Sắp hết hạn (<=7 ngày),{overview.ExpiringSoonSubscriptions}");
        sb.AppendLine($"Đăng ký gói mới,{overview.NewSubscriptions},Gia hạn gói,{overview.Renewals},Tỷ lệ gia hạn,{overview.RenewalRate}%");
        sb.AppendLine($"Mua lẻ truyện,{overview.StoryPurchases},Tổng đơn thành công,{overview.PaidOrders}/{overview.TotalOrders} ({overview.SuccessRate}%)");
        sb.AppendLine();
        sb.AppendLine("Ngày (Giờ VN),Thành viên mới,Độc giả hoạt động (DAU),Lượt đọc chương,Đăng ký gói mới,Gia hạn gói,Mua lẻ truyện,Tổng đơn hàng,Đơn thành công (Paid),Đơn chờ (Pending),Đơn lỗi/hủy/hết hạn");

        foreach (var d in daily)
        {
            sb.AppendLine($"{d.Date:yyyy-MM-dd},{d.NewMembers},{d.ActiveReaders},{d.ChaptersRead},{d.NewSubscriptions},{d.Renewals},{d.StoryPurchases},{d.TotalOrders},{d.PaidOrders},{d.PendingOrders},{d.FailedOrders}");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    // =====================================================================
    // HÀM PHỤ NỘI BỘ
    // =====================================================================
    private async Task<MemberActivitySummaryDto> BuildMemberSummaryAsync(DateRange range)
    {
        var now = DateTime.UtcNow;
        var soonCutoff = now.AddDays(ExpiringSoonWindowDays);

        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.UserId, u.Role, u.AccountStatus, u.CreatedAt })
            .ToListAsync();

        var members = users.Where(u => u.Role == RoleMember).ToList();
        var admins = users.Where(u => u.Role == RoleAdmin).ToList();

        var newRegistrations = members.Count(u => u.CreatedAt >= range.FromUtc && u.CreatedAt < range.ToUtc);

        var readsInPeriod = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.UserId != null && r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .Select(r => r.UserId!.Value)
            .ToListAsync();

        var totalReadsByMembers = readsInPeriod.Count;
        var activeReaders = readsInPeriod.Distinct().Count();
        var avgChaptersPerReader = activeReaders > 0
            ? Math.Round((double)totalReadsByMembers / activeReaders, 2)
            : 0;

        var activeSubUserIds = await GetActiveSubscriberUserIdsAsync(now);

        // Thuê bao sắp hết hạn trong 7 ngày tới
        var expiringSoonUserIds = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.RevokedAt == null && s.StartsAt <= now && s.EndsAt > now && s.EndsAt <= soonCutoff)
            .Select(s => s.Order.UserId)
            .Distinct()
            .ToListAsync();

        // Tất cả người từng mua gói đọc
        var allEverSubUserIdsFromSubs = await _db.UserSubscriptions.AsNoTracking()
            .Select(s => s.Order.UserId)
            .Distinct()
            .ToListAsync();
        var allEverSubUserIdsFromOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderType == TypePlan && o.Status == StatusPaid)
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync();
        var allEverSubUsers = allEverSubUserIdsFromSubs.Concat(allEverSubUserIdsFromOrders).ToHashSet();

        // Churned = từng có gói nhưng hiện không còn gói active
        var churnedCount = allEverSubUsers.Count(uid => !activeSubUserIds.Contains(uid));

        var storyOwnerIdsFromPurchases = await _db.StoryPurchases.AsNoTracking()
            .Where(p => p.RevokedAt == null && (p.ExpiresAt == null || p.ExpiresAt > now))
            .Select(p => p.Order.UserId)
            .Distinct()
            .ToListAsync();

        var storyOwnerIdsFromOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderType == TypeStory && o.Status == StatusPaid)
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync();

        var allStoryOwners = storyOwnerIdsFromPurchases.Concat(storyOwnerIdsFromOrders).ToHashSet();
        var allPayingUsers = allEverSubUsers.Concat(allStoryOwners).ToHashSet();

        var payingRate = members.Count > 0
            ? Math.Round(allPayingUsers.Count * 100.0 / members.Count, 2)
            : 0;

        var retentionRate = allEverSubUsers.Count > 0
            ? Math.Round(activeSubUserIds.Count * 100.0 / allEverSubUsers.Count, 2)
            : 0;

        return new MemberActivitySummaryDto
        {
            From = range.From,
            To = range.To,
            TotalUsers = users.Count,
            TotalMembers = members.Count,
            TotalAdmins = admins.Count,
            ActiveAccounts = members.Count(u => u.AccountStatus == AccountActive),
            LockedAccounts = members.Count(u => u.AccountStatus != AccountActive),
            NewRegistrations = newRegistrations,
            ActiveReadersInPeriod = activeReaders,
            ActiveSubscribersNow = activeSubUserIds.Count,
            ExpiringSoonSubscribers = expiringSoonUserIds.Count,
            ChurnedSubscribers = churnedCount,
            TotalStoryOwners = allStoryOwners.Count,
            PayingMemberRate = payingRate,
            RetentionRate = retentionRate,
            AverageChaptersPerReader = avgChaptersPerReader
        };
    }

    private async Task<HashSet<int>> GetActiveSubscriberUserIdsAsync(DateTime now)
    {
        var fromSubscriptions = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.RevokedAt == null && s.StartsAt <= now && s.EndsAt > now)
            .Select(s => s.Order.UserId)
            .Distinct()
            .ToListAsync();

        var paidPlanOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderType == TypePlan && o.Status == StatusPaid)
            .Select(o => new
            {
                o.UserId,
                StartAt = o.PaidAt ?? o.CreatedAt,
                Months = o.DurationMonths ?? (o.PlanVersion != null ? o.PlanVersion.DurationMonths : 1)
            })
            .ToListAsync();

        var fromOrders = paidPlanOrders
            .Where(o => o.StartAt.AddMonths(Math.Max(1, o.Months)) > now)
            .Select(o => o.UserId);

        return fromSubscriptions.Concat(fromOrders).ToHashSet();
    }

    // Phân loại "đăng ký mới" / "gia hạn" dùng chung luật với PB38 (PlanOrderClassifier):
    // gia hạn = mua lại CÙNG MỘT GÓI khi quyền của đơn trước vẫn còn hạn.
    private async Task<List<ClassifiedPlanOrder>> LoadClassifiedPlanOrdersAsync()
    {
        var paidPlanOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderType == TypePlan && o.Status == StatusPaid)
            .Select(o => new
            {
                o.OrderId,
                o.UserId,
                PlanId = o.PlanVersion != null ? (int?)o.PlanVersion.PlanId : null,
                EffectiveAtUtc = o.PaidAt ?? o.CreatedAt
            })
            .OrderBy(o => o.EffectiveAtUtc)
            .ThenBy(o => o.OrderId)
            .ToListAsync();

        var grants = await PlanOrderClassifier.LoadGrantsAsync(_db);
        var renewalIds = PlanOrderClassifier.FindRenewalOrderIds(
            paidPlanOrders
                .Where(o => o.PlanId.HasValue)
                .Select(o => new PlanOrderClassifier.PlanOrder(o.OrderId, o.UserId, o.PlanId!.Value, o.EffectiveAtUtc)),
            grants);

        return paidPlanOrders
            .Select(o => new ClassifiedPlanOrder(o.OrderId, o.UserId, o.PlanId, o.EffectiveAtUtc, renewalIds.Contains(o.OrderId)))
            .ToList();
    }

    private async Task<TransactionStatusSummaryDto> BuildTransactionSummaryAsync(DateRange range)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Select(o => new
            {
                o.Status,
                o.Amount,
                EffectiveAt = o.Status == StatusPaid && o.PaidAt != null ? o.PaidAt.Value : o.CreatedAt
            })
            .Where(o => o.EffectiveAt >= range.FromUtc && o.EffectiveAt < range.ToUtc)
            .ToListAsync();

        var payments = await _db.Payments.AsNoTracking()
            .Select(p => new
            {
                p.Status,
                EffectiveAt = p.Status == "Succeeded" && p.SucceededAt != null ? p.SucceededAt.Value : p.CreatedAt
            })
            .Where(p => p.EffectiveAt >= range.FromUtc && p.EffectiveAt < range.ToUtc)
            .ToListAsync();

        var totalOrders = orders.Count;
        var breakdown = new List<TransactionStatusStatsDto>();

        foreach (var status in AllOrderStatuses)
        {
            var matchingOrders = orders.Where(o => o.Status == status).ToList();
            var paymentStatus = status == StatusPaid ? "Succeeded" : status;
            var paymentCount = payments.Count(p => p.Status == paymentStatus);

            breakdown.Add(new TransactionStatusStatsDto
            {
                Status = status,
                OrderCount = matchingOrders.Count,
                PaymentCount = paymentCount,
                TotalAmount = matchingOrders.Sum(o => o.Amount),
                Percentage = totalOrders > 0
                    ? Math.Round(matchingOrders.Count * 100.0 / totalOrders, 2)
                    : 0
            });
        }

        var paidCount = breakdown.First(b => b.Status == StatusPaid).OrderCount;
        var pendingCount = breakdown.First(b => b.Status == StatusPending).OrderCount;
        var failedCount = breakdown.First(b => b.Status == StatusFailed).OrderCount;
        var cancelledCount = breakdown.First(b => b.Status == StatusCancelled).OrderCount;
        var expiredCount = breakdown.First(b => b.Status == StatusExpired).OrderCount;

        return new TransactionStatusSummaryDto
        {
            From = range.From,
            To = range.To,
            TotalOrders = totalOrders,
            TotalPayments = payments.Count,
            PaidCount = paidCount,
            PendingCount = pendingCount,
            FailedCount = failedCount,
            CancelledCount = cancelledCount,
            ExpiredCount = expiredCount,
            SuccessRate = totalOrders > 0 ? Math.Round(paidCount * 100.0 / totalOrders, 2) : 0,
            StatusBreakdown = breakdown
        };
    }

    private async Task<List<PlanActivityStatsDto>> BuildPlanStatsAsync(
        DateRange range,
        List<ClassifiedPlanOrder> planEventsInRange)
    {
        var now = DateTime.UtcNow;
        var soonCutoff = now.AddDays(ExpiringSoonWindowDays);

        var plans = await _db.ReadingPlans.AsNoTracking()
            .Select(p => new { p.PlanId, p.Name, p.IsAvailable })
            .OrderBy(p => p.PlanId)
            .ToListAsync();

        var allSubsWithPlan = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.Order.PlanVersion != null)
            .Select(s => new
            {
                PlanId = s.Order.PlanVersion!.PlanId,
                s.Order.UserId,
                s.StartsAt,
                s.EndsAt,
                s.RevokedAt
            })
            .ToListAsync();

        var subsByPlan = allSubsWithPlan.ToLookup(s => s.PlanId);

        var eventsByPlan = planEventsInRange
            .Where(e => e.PlanId.HasValue)
            .ToLookup(e => e.PlanId!.Value);

        return plans.Select(p =>
        {
            var evts = eventsByPlan[p.PlanId].ToList();
            var newSubs = evts.Count(e => !e.IsRenewal);
            var renewals = evts.Count(e => e.IsRenewal);
            var totalPurchases = newSubs + renewals;

            var planSubs = subsByPlan[p.PlanId].ToList();
            var activeUsers = planSubs
                .Where(s => s.RevokedAt == null && s.StartsAt <= now && s.EndsAt > now)
                .Select(s => s.UserId)
                .Distinct()
                .Count();

            var expiringSoon = planSubs
                .Where(s => s.RevokedAt == null && s.StartsAt <= now && s.EndsAt > now && s.EndsAt <= soonCutoff)
                .Select(s => s.UserId)
                .Distinct()
                .Count();

            var expired = planSubs
                .Where(s => s.RevokedAt == null && s.EndsAt <= now)
                .Select(s => s.UserId)
                .Distinct()
                .Count();

            return new PlanActivityStatsDto
            {
                PlanId = p.PlanId,
                PlanName = p.Name,
                IsAvailable = p.IsAvailable,
                NewSubscriptions = newSubs,
                Renewals = renewals,
                TotalPurchases = totalPurchases,
                ActiveSubscribers = activeUsers,
                ExpiringSoonCount = expiringSoon,
                ExpiredCount = expired,
                RenewalRate = totalPurchases > 0
                    ? Math.Round(renewals * 100.0 / totalPurchases, 2)
                    : 0
            };
        })
        .OrderByDescending(p => p.TotalPurchases)
        .ThenBy(p => p.PlanName)
        .ToList();
    }

    private static double GrowthPercent(int current, int previous)
    {
        if (previous <= 0)
            return current > 0 ? 100.0 : 0.0;
        return Math.Round((current - previous) * 100.0 / previous, 2);
    }

    private static DateOnly ToVnDate(DateTime utc)
        => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTime));

    private static int ToVnHour(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTime).Hour;

    private static Dictionary<DateOnly, int> CountByDay(IEnumerable<DateTime> utcTimes)
        => utcTimes
            .GroupBy(ToVnDate)
            .ToDictionary(g => g.Key, g => g.Count());

    private static DateRange ResolveRange(ActivityStatsQuery query)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTime));
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-(DefaultDays - 1));

        if (from > to)
            throw new ArgumentException("Ngày bắt đầu (from) phải trước hoặc bằng ngày kết thúc (to).");
        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"Chỉ xem tối đa {MaxDays} ngày mỗi lần.");

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), VietnamTime);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamTime);
        return new DateRange(from, to, fromUtc, toUtc);
    }

    private static DateRange PreviousRange(DateRange current)
    {
        var days = current.To.DayNumber - current.From.DayNumber + 1;
        var prevTo = current.From.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(days - 1));
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(prevFrom.ToDateTime(TimeOnly.MinValue), VietnamTime);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(prevTo.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamTime);
        return new DateRange(prevFrom, prevTo, fromUtc, toUtc);
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

    private sealed record DateRange(DateOnly From, DateOnly To, DateTime FromUtc, DateTime ToUtc);

    private sealed record ClassifiedPlanOrder(
        int OrderId,
        int UserId,
        int? PlanId,
        DateTime EffectiveAtUtc,
        bool IsRenewal);
}

using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Entitlements;
using ReadManager.Api.DTOs.Library;
using ReadManager.Api.DTOs.Subscriptions;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// BACKEND 2 — QUYỀN ĐỌC, THƯ VIỆN & HOẠT ĐỘNG ĐỘC GIẢ
// PB22: Kiểm tra quyền đọc chương / truyện
// PB23: Truyện đã mua lẻ vẫn đọc được kể cả khi gói hết hạn
// PB27: Thư viện, danh sách gói của tôi, cảnh báo sắp hết hạn & tiến độ đọc tiếp (Continue Reading / Streak)
public interface IEntitlementService
{
    Task<EntitlementCheckResult> CheckStoryAccessAsync(int storyId, int? userId, bool isAdmin = false);
    Task<EntitlementCheckResult> CheckChapterAccessAsync(int chapterId, int? userId, bool isAdmin = false);

    // Cấp quyền (hỗ trợ Payment webhook / Order fulfillment gọi, an toàn lặp với cùng orderId)
    Task<UserSubscription> GrantSubscriptionAsync(int orderId);
    Task<StoryPurchase> GrantStoryPurchaseAsync(int orderId);

    // PB27 — Thư viện, gói của tôi & thống kê đọc cá nhân
    Task<UserSubscriptionSummaryDto> GetMySubscriptionsAsync(int userId);
    Task<UserLibraryDto> GetMyLibraryAsync(int userId);
    Task<MyReadingActivityDto> GetMyReadingActivityAsync(int userId);
}

public class EntitlementService : IEntitlementService
{
    private const string Free = "Free";
    private const string ScopeAll = "All";
    private const string ScopeSelected = "Selected";
    private const string PublicVisibility = "Public";
    private const string Published = "Published";
    private const int ExpiringWarningDays = 7;

    private static readonly TimeZoneInfo VietnamTime = FindVietnamTimeZone();

    private readonly AppDbContext _db;

    public EntitlementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<EntitlementCheckResult> CheckStoryAccessAsync(int storyId, int? userId, bool isAdmin = false)
    {
        if (isAdmin)
            return new EntitlementCheckResult { CanRead = true, AccessType = "Admin" };

        var story = await _db.Stories.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StoryId == storyId);

        if (story is null)
            return new EntitlementCheckResult { CanRead = false, Reason = "NotFound" };

        if (story.AccessPolicy == Free)
            return new EntitlementCheckResult { CanRead = true, AccessType = "Free" };

        if (!userId.HasValue)
            return new EntitlementCheckResult { CanRead = false, Reason = "LoginRequired" };

        var now = DateTime.UtcNow;

        // 1. PB23 — Đã mua riêng truyện thì luôn đọc được (kể cả khi không có gói hoặc gói đã hết hạn)
        var hasPurchased = await _db.StoryPurchases.AsNoTracking()
            .AnyAsync(p => p.Order.UserId == userId.Value
                        && p.Order.StoryId == storyId
                        && p.RevokedAt == null
                        && (p.ExpiresAt == null || p.ExpiresAt > now));

        if (hasPurchased)
            return new EntitlementCheckResult { CanRead = true, AccessType = "Purchased" };

        // 2. PB22 — Kiểm tra quyền từ gói đăng ký còn hiệu lực
        var activeSubs = await _db.UserSubscriptions.AsNoTracking()
            .Include(s => s.Order)
                .ThenInclude(o => o.PlanVersion!)
                    .ThenInclude(v => v.PlanVersionStories)
            .Where(s => s.Order.UserId == userId.Value
                     && s.RevokedAt == null
                     && s.StartsAt <= now
                     && s.EndsAt > now)
            .ToListAsync();

        foreach (var sub in activeSubs)
        {
            var version = sub.Order.PlanVersion;
            if (version is null)
                continue;

            if (version.ScopeType == ScopeAll)
                return new EntitlementCheckResult { CanRead = true, AccessType = "Subscription" };

            if (version.ScopeType == ScopeSelected &&
                version.PlanVersionStories.Any(p => p.StoryId == storyId))
            {
                return new EntitlementCheckResult { CanRead = true, AccessType = "Subscription" };
            }
        }

        // 3. Nếu từng có gói bao phủ truyện này nhưng đã hết hạn → báo SubscriptionExpired
        var hadCoveringSub = await _db.UserSubscriptions.AsNoTracking()
            .Include(s => s.Order)
                .ThenInclude(o => o.PlanVersion!)
                    .ThenInclude(v => v.PlanVersionStories)
            .Where(s => s.Order.UserId == userId.Value && s.EndsAt <= now)
            .AnyAsync(s => s.Order.PlanVersion != null &&
                           (s.Order.PlanVersion.ScopeType == ScopeAll ||
                            s.Order.PlanVersion.PlanVersionStories.Any(p => p.StoryId == storyId)));

        if (hadCoveringSub)
            return new EntitlementCheckResult { CanRead = false, Reason = "SubscriptionExpired" };

        return new EntitlementCheckResult
        {
            CanRead = false,
            Reason = story.AccessPolicy == "Paid" ? "StoryPurchaseRequired" : "SubscriptionRequired"
        };
    }

    public async Task<EntitlementCheckResult> CheckChapterAccessAsync(int chapterId, int? userId, bool isAdmin = false)
    {
        if (isAdmin)
            return new EntitlementCheckResult { CanRead = true, AccessType = "Admin" };

        var chapter = await _db.Chapters.AsNoTracking()
            .Include(c => c.Story)
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);

        if (chapter is null)
            return new EntitlementCheckResult { CanRead = false, Reason = "NotFound" };

        // Chương miễn phí hoặc truyện miễn phí
        if (chapter.AccessLevel == Free || chapter.Story.AccessPolicy == Free)
            return new EntitlementCheckResult { CanRead = true, AccessType = "Free" };

        return await CheckStoryAccessAsync(chapter.StoryId, userId, isAdmin);
    }

    // Cấp gói đăng ký khi đơn hàng thanh toán thành công
    public async Task<UserSubscription> GrantSubscriptionAsync(int orderId)
    {
        var existing = await _db.UserSubscriptions
            .Include(s => s.Order)
            .FirstOrDefaultAsync(s => s.OrderId == orderId);

        if (existing is not null)
            return existing;

        var order = await _db.Orders
            .Include(o => o.PlanVersion)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order is null)
            throw new InvalidOperationException($"Không tìm thấy đơn hàng {orderId}.");

        if (order.OrderType != "Plan" || !order.PlanVersionId.HasValue)
            throw new InvalidOperationException($"Đơn hàng {orderId} không phải là đơn mua gói.");

        var months = order.DurationMonths ?? order.PlanVersion?.DurationMonths ?? 1;
        var now = DateTime.UtcNow;

        // Quy tắc gia hạn: nếu người dùng đang có gói còn hạn, kỳ mới nối tiếp EndsAt của gói cũ
        var latestActiveEnd = await _db.UserSubscriptions
            .Where(s => s.Order.UserId == order.UserId && s.RevokedAt == null && s.EndsAt > now)
            .MaxAsync(s => (DateTime?)s.EndsAt);

        var startsAt = latestActiveEnd.HasValue && latestActiveEnd.Value > now
            ? latestActiveEnd.Value
            : now;
        var endsAt = startsAt.AddMonths(months);

        var sub = new UserSubscription
        {
            OrderId = orderId,
            StartsAt = startsAt,
            EndsAt = endsAt
        };

        _db.UserSubscriptions.Add(sub);
        await _db.SaveChangesAsync();
        return sub;
    }

    // Cấp quyền mua riêng truyện khi đơn hàng thanh toán thành công
    public async Task<StoryPurchase> GrantStoryPurchaseAsync(int orderId)
    {
        var existing = await _db.StoryPurchases
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.OrderId == orderId);

        if (existing is not null)
            return existing;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
        if (order is null)
            throw new InvalidOperationException($"Không tìm thấy đơn hàng {orderId}.");

        if (order.OrderType != "Story" || !order.StoryId.HasValue)
            throw new InvalidOperationException($"Đơn hàng {orderId} không phải là đơn mua truyện.");

        var purchase = new StoryPurchase
        {
            OrderId = orderId,
            GrantedAt = DateTime.UtcNow,
            ExpiresAt = null // Vĩnh viễn
        };

        _db.StoryPurchases.Add(purchase);
        await _db.SaveChangesAsync();
        return purchase;
    }

    // PB27 — Lấy thông tin gói của tôi (kèm cảnh báo sắp hết hạn thực tế)
    public async Task<UserSubscriptionSummaryDto> GetMySubscriptionsAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var subs = await _db.UserSubscriptions.AsNoTracking()
            .Include(s => s.Order)
                .ThenInclude(o => o.PlanVersion!)
                    .ThenInclude(v => v.Plan)
            .Where(s => s.Order.UserId == userId)
            .OrderByDescending(s => s.EndsAt)
            .ToListAsync();

        var dtos = subs.Select(s =>
        {
            var plan = s.Order.PlanVersion?.Plan;
            var planName = plan?.Name ?? s.Order.ItemName;
            var isActive = s.RevokedAt == null && s.StartsAt <= now && s.EndsAt > now;
            var days = isActive ? Math.Max(0, (int)Math.Ceiling((s.EndsAt - now).TotalDays)) : 0;
            var isExpiringSoon = isActive && days <= ExpiringWarningDays;

            var badge = s.RevokedAt != null
                ? "Đã thu hồi"
                : isExpiringSoon
                    ? "Sắp hết hạn"
                    : isActive
                        ? "Đang hoạt động"
                        : "Đã hết hạn";

            return new UserSubscriptionDto
            {
                SubscriptionId = s.SubscriptionId,
                OrderId = s.OrderId,
                PlanId = plan?.PlanId,
                PlanVersionId = s.Order.PlanVersionId,
                PlanName = planName,
                ScopeType = s.Order.PlanVersion?.ScopeType ?? ScopeSelected,
                StartsAt = s.StartsAt,
                EndsAt = s.EndsAt,
                RevokedAt = s.RevokedAt,
                IsActive = isActive,
                DaysRemaining = days,
                IsExpiringSoon = isExpiringSoon,
                StatusBadge = badge,
                CanRenew = plan?.IsAvailable ?? true
            };
        }).ToList();

        var active = dtos.FirstOrDefault(d => d.IsActive);
        var hasAlert = active is { IsExpiringSoon: true };
        var alertMsg = hasAlert
            ? $"Gói \"{active!.PlanName}\" của bạn chỉ còn {active.DaysRemaining} ngày sử dụng. Hãy gia hạn sớm để đọc truyện không gián đoạn!"
            : null;

        return new UserSubscriptionSummaryDto
        {
            ActiveSubscription = active,
            HasExpiringAlert = hasAlert,
            AlertMessage = alertMsg,
            History = dtos
        };
    }

    // PB27 — Thư viện của tôi (truyện mua lẻ + gói đang dùng)
    public async Task<UserLibraryDto> GetMyLibraryAsync(int userId)
    {
        var now = DateTime.UtcNow;

        var purchases = await _db.StoryPurchases.AsNoTracking()
            .Include(p => p.Order)
                .ThenInclude(o => o.Story)
            .Where(p => p.Order.UserId == userId
                     && p.RevokedAt == null
                     && (p.ExpiresAt == null || p.ExpiresAt > now)
                     && p.Order.Story != null)
            .OrderByDescending(p => p.GrantedAt)
            .Select(p => new PurchasedStoryItemDto
            {
                StoryId = p.Order.Story!.StoryId,
                Title = p.Order.Story.Title,
                Slug = p.Order.Story.Slug,
                AuthorName = p.Order.Story.AuthorName,
                CoverUrl = p.Order.Story.CoverUrl,
                GrantedAt = p.GrantedAt,
                ExpiresAt = p.ExpiresAt
            })
            .ToListAsync();

        var subs = await GetMySubscriptionsAsync(userId);

        return new UserLibraryDto
        {
            PurchasedStories = purchases,
            Subscriptions = subs
        };
    }

    // Tính năng thực tế giống Webnovel/Waka: Chuỗi ngày đọc liên tiếp (Reading Streak) + Đọc tiếp (Continue Reading)
    public async Task<MyReadingActivityDto> GetMyReadingActivityAsync(int userId)
    {
        var now = DateTime.UtcNow;

        var reads = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReadAt)
            .Select(r => new { r.StoryId, r.ChapterId, r.ReadAt })
            .ToListAsync();

        // Tính chuỗi ngày đọc liên tiếp (theo lịch giờ Việt Nam)
        var readDates = reads
            .Select(r => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.ReadAt, DateTimeKind.Utc), VietnamTime)))
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        var todayVn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, VietnamTime));
        var (currentStreak, longestStreak) = CalculateStreaks(readDates, todayVn);

        var uniqueChapters = reads.Select(r => r.ChapterId).Distinct().Count();
        var uniqueStoryIds = reads.Select(r => r.StoryId).Distinct().ToList();

        // Top 3 thể loại yêu thích của độc giả
        var favGenres = new List<string>();
        if (uniqueStoryIds.Count > 0)
        {
            favGenres = await _db.StoryGenres.AsNoTracking()
                .Where(sg => uniqueStoryIds.Contains(sg.StoryId))
                .GroupBy(sg => sg.Genre.Name)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToListAsync();
        }

        // Danh sách "Đọc tiếp" (Continue Reading) — chương vừa đọc gần nhất của mỗi bộ truyện
        var latestByStory = reads
            .GroupBy(r => r.StoryId)
            .Select(g => g.First())
            .Take(10)
            .ToList();

        var continueList = new List<ContinueReadingItemDto>();
        if (latestByStory.Count > 0)
        {
            var storyIds = latestByStory.Select(x => x.StoryId).ToList();
            var chapterIds = latestByStory.Select(x => x.ChapterId).ToList();

            var stories = await _db.Stories.AsNoTracking()
                .Where(s => storyIds.Contains(s.StoryId) && s.Visibility == PublicVisibility)
                .ToDictionaryAsync(s => s.StoryId);

            var chapters = await _db.Chapters.AsNoTracking()
                .Where(c => chapterIds.Contains(c.ChapterId))
                .ToDictionaryAsync(c => c.ChapterId);

            var pubCounts = await _db.Chapters.AsNoTracking()
                .Where(c => storyIds.Contains(c.StoryId) && c.PublicationStatus == Published)
                .GroupBy(c => c.StoryId)
                .Select(g => new { StoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StoryId, x => x.Count);

            foreach (var item in latestByStory)
            {
                if (!stories.TryGetValue(item.StoryId, out var story) ||
                    !chapters.TryGetValue(item.ChapterId, out var ch))
                    continue;

                var totalCh = Math.Max(1, pubCounts.GetValueOrDefault(story.StoryId, 1));
                var progress = Math.Min(100.0, Math.Round(ch.ChapterNumber * 100.0 / totalCh, 1));

                continueList.Add(new ContinueReadingItemDto
                {
                    StoryId = story.StoryId,
                    StoryTitle = story.Title,
                    StorySlug = story.Slug,
                    CoverUrl = story.CoverUrl,
                    LastChapterId = ch.ChapterId,
                    LastChapterNumber = ch.ChapterNumber,
                    LastChapterTitle = ch.Title,
                    TotalPublishedChapters = totalCh,
                    ProgressPercent = progress,
                    LastReadAtUtc = item.ReadAt
                });
            }
        }

        // Phân hạng thành viên
        var hasActiveSub = await _db.UserSubscriptions.AsNoTracking()
            .AnyAsync(s => s.Order.UserId == userId && s.RevokedAt == null && s.StartsAt <= now && s.EndsAt > now);
        var hasPurchase = await _db.StoryPurchases.AsNoTracking()
            .AnyAsync(p => p.Order.UserId == userId && p.RevokedAt == null);

        var tier = hasActiveSub
            ? "VIP Thuê Bao"
            : hasPurchase
                ? "Người Mua Truyện"
                : uniqueChapters >= 10
                    ? "Độc Giả Tích Cực"
                    : "Thành Viên Mới";

        return new MyReadingActivityDto
        {
            UserId = userId,
            MemberTier = tier,
            CurrentStreakDays = currentStreak,
            LongestStreakDays = longestStreak,
            TotalChaptersRead = reads.Count,
            UniqueChaptersRead = uniqueChapters,
            UniqueStoriesRead = uniqueStoryIds.Count,
            FavoriteGenres = favGenres,
            ContinueReading = continueList
        };
    }

    private static (int CurrentStreak, int LongestStreak) CalculateStreaks(List<DateOnly> sortedDescDates, DateOnly today)
    {
        if (sortedDescDates.Count == 0)
            return (0, 0);

        // Current streak: tính từ hôm nay hoặc hôm qua lùi về trước
        var current = 0;
        var expected = sortedDescDates[0] == today
            ? today
            : sortedDescDates[0] == today.AddDays(-1)
                ? today.AddDays(-1)
                : (DateOnly?)null;

        if (expected.HasValue)
        {
            foreach (var d in sortedDescDates)
            {
                if (d == expected.Value)
                {
                    current++;
                    expected = expected.Value.AddDays(-1);
                }
                else if (d < expected.Value)
                {
                    break;
                }
            }
        }

        // Longest streak
        var longest = 1;
        var run = 1;
        for (int i = 1; i < sortedDescDates.Count; i++)
        {
            if (sortedDescDates[i - 1].DayNumber - sortedDescDates[i].DayNumber == 1)
            {
                run++;
                if (run > longest)
                    longest = run;
            }
            else
            {
                run = 1;
            }
        }

        return (current, Math.Max(current, longest));
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

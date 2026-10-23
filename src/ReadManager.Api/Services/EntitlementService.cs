using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Entitlements;
using ReadManager.Api.DTOs.Library;
using ReadManager.Api.DTOs.Subscriptions;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// BACKEND 2 (Sprint 2) / BACKEND 3 (Sprint 3)
// PB22: Kiểm tra quyền đọc chương / truyện
// PB23: Truyện đã mua lẻ vẫn đọc được kể cả khi gói hết hạn
// PB27: Thư viện & danh sách gói của tôi
public interface IEntitlementService
{
    Task<EntitlementCheckResult> CheckStoryAccessAsync(int storyId, int? userId, bool isAdmin = false);
    Task<EntitlementCheckResult> CheckChapterAccessAsync(int chapterId, int? userId, bool isAdmin = false);

    // Cấp quyền (hỗ trợ Payment webhook / Order fulfillment gọi, an toàn lặp với cùng orderId)
    Task<UserSubscription> GrantSubscriptionAsync(int orderId);
    Task<StoryPurchase> GrantStoryPurchaseAsync(int orderId);

    // PB27 — Thư viện và gói của tôi
    Task<UserSubscriptionSummaryDto> GetMySubscriptionsAsync(int userId);
    Task<UserLibraryDto> GetMyLibraryAsync(int userId);
}

public class EntitlementService : IEntitlementService
{
    private const string Free = "Free";
    private const string ScopeAll = "All";
    private const string ScopeSelected = "Selected";
    private const string PublicVisibility = "Public";
    private const string Published = "Published";

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

    // PB27 — Lấy thông tin gói của tôi
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
                DaysRemaining = days
            };
        }).ToList();

        var active = dtos.FirstOrDefault(d => d.IsActive);
        return new UserSubscriptionSummaryDto
        {
            ActiveSubscription = active,
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
}

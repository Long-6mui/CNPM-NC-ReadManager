using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Library;
using ReadManager.Api.DTOs.Subscriptions;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// BACKEND 2 — QUYỀN ĐỌC
//   PB22: kiểm tra quyền trước khi cho đọc chương trả phí
//   PB23: gói hết hạn vẫn giữ quyền đọc truyện đã mua riêng
//   PB19–21: cấp quyền / gia hạn (Backend 3, 4 gọi sau khi thanh toán thành công)
//
// QUY TẮC KIỂM TRA (theo thứ tự):
//   1. Truyện Free, hoặc chương Free            → đọc được
//   2. Chưa đăng nhập                           → LoginRequired
//   3. Đã mua riêng truyện                      → đọc được (KHÔNG phụ thuộc gói, PB23)
//   4. Có gói còn hạn bao phủ truyện            → đọc được
//   5. Còn lại                                  → không đọc được
public interface IEntitlementService
{
    // Dùng nội bộ (ChapterService): quyền của người dùng với 1 truyện trả phí.
    Task<StoryAccessResult> ResolveAsync(int? userId, int storyId, string storyAccessPolicy);

    // PB22 — API kiểm tra quyền. Trả null = truyện/chương không tồn tại hoặc chưa công khai.
    Task<AccessDecisionDto?> CheckStoryAsync(int? userId, int storyId, bool isAdmin);
    Task<AccessDecisionDto?> CheckChapterAsync(int? userId, int chapterId, bool isAdmin);

    // PB19–21 — CẤP QUYỀN. Chỉ gọi SAU KHI thanh toán thành công. Gọi lặp với cùng orderId vẫn an toàn.
    Task<GrantResultDto> GrantSubscriptionAsync(int userId, int planVersionId, int? orderId);
    Task<GrantResultDto> GrantStoryAccessAsync(int userId, int storyId, decimal pricePaid, int? orderId);
}

// Kết quả nội bộ của việc tra quyền
public record StoryAccessResult(
    bool HasAccess,
    string Source,                 // Free | Purchase | Subscription | None
    string? Reason = null,
    string? PlanName = null,
    DateTime? SubscriptionEndsAt = null);

public class EntitlementService : IEntitlementService
{
    private const string Free = "Free";
    private const string Published = "Published";
    private const string PublicVisibility = "Public";
    private const string ActiveStatus = "Active";

    private readonly AppDbContext _db;

    public EntitlementService(AppDbContext db)
    {
        _db = db;
    }

    // ===== PB22 / PB23 — KIỂM TRA QUYỀN =====

    public async Task<StoryAccessResult> ResolveAsync(int? userId, int storyId, string storyAccessPolicy)
    {
        // 1. Truyện miễn phí
        if (storyAccessPolicy == Free)
            return new StoryAccessResult(true, Free);

        // 2. Chưa đăng nhập
        if (userId is null)
            return new StoryAccessResult(false, "None", "LoginRequired");

        var uid = userId.Value;

        // 3. Mua riêng — không xét gói (PB23: gói hết hạn vẫn đọc được truyện đã mua)
        var purchased = await _db.UserStoryAccesses.AsNoTracking()
            .AnyAsync(a => a.UserId == uid && a.StoryId == storyId);
        if (purchased)
            return new StoryAccessResult(true, "Purchase");

        // 4. Gói còn hạn có phạm vi bao phủ truyện này
        var now = DateTime.UtcNow;
        var active = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.UserId == uid && s.Status == ActiveStatus && s.StartAt <= now && s.EndAt > now)
            .Where(s => s.PlanVersion.ScopeType == "All"
                        || s.PlanVersion.PlanVersionStories.Any(p => p.StoryId == storyId))
            .OrderByDescending(s => s.EndAt)
            .Select(s => new { s.EndAt, PlanName = s.PlanVersion.Plan.Name })
            .FirstOrDefaultAsync();
        if (active is not null)
            return new StoryAccessResult(true, "Subscription", null, active.PlanName, active.EndAt);

        // 5. Không có quyền — phân biệt "gói đã hết hạn" để giao diện nhắc gia hạn
        var hadExpired = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.UserId == uid && s.Status == ActiveStatus && s.EndAt <= now)
            .AnyAsync(s => s.PlanVersion.ScopeType == "All"
                           || s.PlanVersion.PlanVersionStories.Any(p => p.StoryId == storyId));

        return new StoryAccessResult(false, "None", hadExpired ? "SubscriptionExpired" : "NotPurchased");
    }

    public async Task<AccessDecisionDto?> CheckStoryAsync(int? userId, int storyId, bool isAdmin)
    {
        var story = await _db.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.StoryId == storyId);
        if (story is null || (!isAdmin && story.Visibility != PublicVisibility))
            return null;

        if (isAdmin)
            return ToDto(storyId, null, new StoryAccessResult(true, "Admin"));

        return ToDto(storyId, null, await ResolveAsync(userId, storyId, story.AccessPolicy));
    }

    public async Task<AccessDecisionDto?> CheckChapterAsync(int? userId, int chapterId, bool isAdmin)
    {
        var chapter = await _db.Chapters.AsNoTracking()
            .Include(c => c.Story)
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        if (chapter is null)
            return null;

        // Độc giả chỉ được hỏi về chương đã công khai của truyện đã công khai (giống ChapterService)
        if (!isAdmin && (chapter.Story.Visibility != PublicVisibility || chapter.PublicationStatus != Published))
            return null;

        if (isAdmin)
            return ToDto(chapter.StoryId, chapterId, new StoryAccessResult(true, "Admin"));

        // Chương hẹn giờ chưa tới giờ: dù đã trả tiền cũng chưa đọc được
        if (chapter.ScheduledAt is { } scheduled && ToUtc(scheduled) > DateTime.UtcNow)
            return ToDto(chapter.StoryId, chapterId, new StoryAccessResult(false, "None", "NotReleased"));

        // Chương miễn phí
        if (chapter.AccessLevel == Free)
            return ToDto(chapter.StoryId, chapterId, new StoryAccessResult(true, Free));

        return ToDto(chapter.StoryId, chapterId,
            await ResolveAsync(userId, chapter.StoryId, chapter.Story.AccessPolicy));
    }

    // ===== PB19–21 — CẤP QUYỀN =====

    public async Task<GrantResultDto> GrantSubscriptionAsync(int userId, int planVersionId, int? orderId)
    {
        // Chống cấp trùng: cùng đơn hàng đã cấp rồi → trả lại kết quả cũ (callback lặp, admin bấm khôi phục 2 lần)
        if (orderId is not null)
        {
            var existing = await _db.UserSubscriptions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderId == orderId);
            if (existing is not null)
            {
                // Idempotency must not treat another user's/order's entitlement as this request's success.
                if (existing.UserId != userId || existing.PlanVersionId != planVersionId)
                    return Fail("OrderAlreadyUsed");
                return SubscriptionResult(existing, "AlreadyGranted");
            }
        }

        if (!await _db.Users.AnyAsync(u => u.UserId == userId))
            return Fail("UserNotFound");

        var version = await _db.ReadingPlanVersions.AsNoTracking()
            .Include(v => v.Plan)
            .FirstOrDefaultAsync(v => v.PlanVersionId == planVersionId);
        if (version is null)
            return Fail("PlanVersionNotFound");

        // PB18: gói đã ngừng cung cấp thì không bán/gia hạn nữa (người đang dùng vẫn giữ tới hết hạn)
        if (!version.Plan.IsAvailable)
            return Fail("PlanUnavailable");

        // QUY TẮC GIA HẠN (PB20):
        //   - Gia hạn SỚM (gói cùng loại còn hạn): kỳ mới nối tiếp ngay sau ngày hết hạn hiện tại → không mất ngày đã trả.
        //   - Gia hạn MUỘN (đã hết hạn) hoặc mua lần đầu: kỳ mới bắt đầu từ lúc thanh toán thành công.
        var now = DateTime.UtcNow;
        var latestEnd = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ActiveStatus
                        && s.PlanVersion.PlanId == version.PlanId && s.EndAt > now)
            .MaxAsync(s => (DateTime?)s.EndAt);

        var renewing = latestEnd is not null;
        var start = latestEnd ?? now;

        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanVersionId = planVersionId,
            StartAt = start,
            EndAt = start.AddMonths(version.DurationMonths),
            Status = ActiveStatus,
            OrderId = orderId,
            CreatedAt = now
        };
        _db.UserSubscriptions.Add(subscription);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hai callback cùng lúc: một bên thắng ở chỉ mục duy nhất OrderId → bên thua trả kết quả của bên thắng
            _db.Entry(subscription).State = EntityState.Detached;
            if (orderId is not null)
            {
                var winner = await _db.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.OrderId == orderId);
                if (winner is not null)
                    return SubscriptionResult(winner, "AlreadyGranted");
            }
            throw;
        }

        return SubscriptionResult(subscription, renewing ? "Renewed" : "Activated");
    }

    public async Task<GrantResultDto> GrantStoryAccessAsync(int userId, int storyId, decimal pricePaid, int? orderId)
    {
        if (orderId is not null)
        {
            var byOrder = await _db.UserStoryAccesses.AsNoTracking().FirstOrDefaultAsync(a => a.OrderId == orderId);
            if (byOrder is not null)
            {
                // The same order may only confirm the same user's purchase of the same story.
                if (byOrder.UserId != userId || byOrder.StoryId != storyId)
                    return Fail("OrderAlreadyUsed");
                return AccessResult(byOrder, "AlreadyGranted");
            }
        }

        if (pricePaid < 0)
            return Fail("InvalidAmount");

        if (!await _db.Users.AnyAsync(u => u.UserId == userId))
            return Fail("UserNotFound");

        var story = await _db.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.StoryId == storyId);
        if (story is null)
            return Fail("StoryNotFound");
        if (story.AccessPolicy == Free)
            return Fail("StoryNotPaid");   // truyện miễn phí không cần mua

        // Đã sở hữu (mua bằng đơn khác) → không cấp thêm, không báo lỗi
        var owned = await _db.UserStoryAccesses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.StoryId == storyId);
        if (owned is not null)
            return AccessResult(owned, "AlreadyOwned");

        var access = new UserStoryAccess
        {
            UserId = userId,
            StoryId = storyId,
            PricePaid = pricePaid,
            PurchasedAt = DateTime.UtcNow,
            OrderId = orderId
        };
        _db.UserStoryAccesses.Add(access);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Đụng chỉ mục duy nhất (UserId, StoryId) hoặc OrderId do 2 yêu cầu song song
            _db.Entry(access).State = EntityState.Detached;
            var winner = await _db.UserStoryAccesses.AsNoTracking().FirstOrDefaultAsync(a =>
                (orderId != null && a.OrderId == orderId) || (a.UserId == userId && a.StoryId == storyId));
            if (winner is not null)
                return AccessResult(winner, "AlreadyOwned");
            throw;
        }

        return AccessResult(access, "Granted");
    }

    // ===== Hàm phụ =====

    private static AccessDecisionDto ToDto(int storyId, int? chapterId, StoryAccessResult r) => new()
    {
        StoryId = storyId,
        ChapterId = chapterId,
        HasAccess = r.HasAccess,
        Source = r.Source,
        Reason = r.Reason,
        PlanName = r.PlanName,
        SubscriptionEndsAt = r.SubscriptionEndsAt
    };

    private static GrantResultDto Fail(string outcome) => new() { Succeeded = false, Outcome = outcome };

    private static GrantResultDto SubscriptionResult(UserSubscription s, string outcome) => new()
    {
        Succeeded = true,
        Outcome = outcome,
        SubscriptionId = s.SubscriptionId,
        StartAt = s.StartAt,
        EndAt = s.EndAt
    };

    private static GrantResultDto AccessResult(UserStoryAccess a, string outcome) => new()
    {
        Succeeded = true,
        Outcome = outcome,
        AccessId = a.AccessId
    };

    // Giống cách ChapterService hiểu ScheduledAt
    private static DateTime ToUtc(DateTime value)
        => value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Library;
using ReadManager.Api.DTOs.Subscriptions;

namespace ReadManager.Api.Services;

// BACKEND 2 — PB27: XEM GÓI ĐANG DÙNG, HẠN HIỆU LỰC VÀ TRUYỆN ĐÃ MUA
public interface ISubscriptionService
{
    Task<MySubscriptionsDto> GetMySubscriptionsAsync(int userId);
    Task<List<PurchasedStoryDto>> GetMyPurchasesAsync(int userId);
    Task<MyLibraryDto> GetMyLibraryAsync(int userId);
}

public class SubscriptionService : ISubscriptionService
{
    private const string PublicVisibility = "Public";

    private readonly AppDbContext _db;

    public SubscriptionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MySubscriptionsDto> GetMySubscriptionsAsync(int userId)
    {
        var now = DateTime.UtcNow;

        var rows = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartAt)
            .Select(s => new
            {
                s.SubscriptionId,
                s.PlanVersion.PlanId,
                PlanName = s.PlanVersion.Plan.Name,
                s.PlanVersionId,
                s.PlanVersion.VersionNumber,
                s.PlanVersion.Price,
                s.PlanVersion.DurationMonths,
                s.PlanVersion.ScopeType,
                StoryCount = s.PlanVersion.PlanVersionStories.Count,
                s.StartAt,
                s.EndAt,
                s.Status
            })
            .ToListAsync();

        var all = rows.Select(r =>
        {
            var status = ResolveStatus(r.Status, r.StartAt, r.EndAt, now);
            return new SubscriptionDto
            {
                SubscriptionId = r.SubscriptionId,
                PlanId = r.PlanId,
                PlanName = r.PlanName,
                PlanVersionId = r.PlanVersionId,
                VersionNumber = r.VersionNumber,
                Price = r.Price,
                DurationMonths = r.DurationMonths,
                ScopeType = r.ScopeType,
                StoryCount = r.ScopeType == "All" ? null : r.StoryCount,
                StartAt = r.StartAt,
                EndAt = r.EndAt,
                Status = status,
                DaysRemaining = status == "Active" ? (int)Math.Ceiling((r.EndAt - now).TotalDays) : 0
            };
        }).ToList();

        var current = all.Where(s => s.Status is "Active" or "Upcoming").OrderBy(s => s.StartAt).ToList();
        var history = all.Where(s => s.Status is "Expired" or "Revoked").OrderByDescending(s => s.EndAt).ToList();
        var hasActive = current.Any(s => s.Status == "Active");

        return new MySubscriptionsDto
        {
            HasActive = hasActive,
            ActiveUntil = hasActive ? current.Max(s => s.EndAt) : null,
            Current = current,
            History = history
        };
    }

    public async Task<List<PurchasedStoryDto>> GetMyPurchasesAsync(int userId)
    {
        return await _db.UserStoryAccesses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.PurchasedAt)
            .Select(a => new PurchasedStoryDto
            {
                StoryId = a.StoryId,
                Title = a.Story.Title,
                Slug = a.Story.Slug,
                AuthorName = a.Story.AuthorName,
                CoverUrl = a.Story.CoverUrl,
                PricePaid = a.PricePaid,
                PurchasedAt = a.PurchasedAt,
                IsAvailable = a.Story.Visibility == PublicVisibility
            })
            .ToListAsync();
    }

    public async Task<MyLibraryDto> GetMyLibraryAsync(int userId)
    {
        // DbContext không chạy song song được → gọi lần lượt
        var subscriptions = await GetMySubscriptionsAsync(userId);
        var purchases = await GetMyPurchasesAsync(userId);
        return new MyLibraryDto { Subscriptions = subscriptions, PurchasedStories = purchases };
    }

    private static string ResolveStatus(string stored, DateTime startAt, DateTime endAt, DateTime now)
    {
        if (stored == "Revoked") return "Revoked";
        if (endAt <= now) return "Expired";
        if (startAt > now) return "Upcoming";
        return "Active";
    }
}

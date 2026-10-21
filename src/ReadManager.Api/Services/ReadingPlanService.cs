using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.ReadingPlans;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

public interface IReadingPlanService
{
    Task<PlanPageDto> ListAsync(bool admin, int page, int pageSize);
    Task<ReadingPlanDto?> GetAsync(int id, bool admin);
    Task<List<PlanVersionDto>?> VersionsAsync(int id);
    Task<ReadingPlanDto> CreateAsync(CreateReadingPlanDto dto);
    Task<PlanVersionDto?> AddVersionAsync(int id, CreatePlanVersionDto dto);
    Task<bool> RenameAsync(int id, RenameReadingPlanDto dto);
    Task<bool> SetAvailabilityAsync(int id, bool available);
}

public class ReadingPlanService(AppDbContext db) : IReadingPlanService
{
    private IQueryable<ReadingPlan> Plans(bool admin)
    {
        var now = DateTime.UtcNow;
        var query = db.ReadingPlans.AsNoTracking();
        return admin ? query : query.Where(p => p.IsAvailable &&
            p.Versions.Any(v => v.PublishedAt != null && v.PublishedAt <= now));
    }

    private static PlanVersionDto Map(ReadingPlanVersion v) => new(v.PlanVersionId, v.VersionNumber,
        v.Price, v.DurationMonths, v.ScopeType, v.CreatedAt, v.PublishedAt,
        v.PlanVersionStories.Select(s => s.StoryId).OrderBy(id => id).ToList());

    private async Task<Dictionary<int, PlanVersionDto>> CurrentVersionsAsync(List<int> ids)
    {
        var now = DateTime.UtcNow;
        var query = db.ReadingPlanVersions.AsNoTracking()
            .Where(v => ids.Contains(v.PlanId) && v.PublishedAt != null && v.PublishedAt <= now);
        // Fetch only the latest published version for each plan, not its full history.
        var versions = await query.Where(v => !db.ReadingPlanVersions.Any(newer =>
                newer.PlanId == v.PlanId && newer.VersionNumber > v.VersionNumber &&
                newer.PublishedAt != null && newer.PublishedAt <= now))
            .Include(v => v.PlanVersionStories).ToListAsync();
        return versions.ToDictionary(v => v.PlanId, Map);
    }

    public async Task<PlanPageDto> ListAsync(bool admin, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Plans(admin);
        var count = await query.CountAsync();
        var skip = ((long)page - 1) * pageSize;
        if (skip >= count) return new([], count, page, pageSize);
        var plans = await query.OrderByDescending(p => p.PlanId).Skip((int)skip).Take(pageSize).ToListAsync();
        var versions = await CurrentVersionsAsync(plans.Select(p => p.PlanId).ToList());
        return new(plans.Select(p => new ReadingPlanDto(p.PlanId, p.Name, p.IsAvailable, p.CreatedAt,
            versions.GetValueOrDefault(p.PlanId))).ToList(), count, page, pageSize);
    }

    public async Task<ReadingPlanDto?> GetAsync(int id, bool admin)
    {
        var plan = await Plans(admin).SingleOrDefaultAsync(p => p.PlanId == id);
        if (plan is null) return null;
        var versions = await CurrentVersionsAsync([id]);
        return new(plan.PlanId, plan.Name, plan.IsAvailable, plan.CreatedAt, versions.GetValueOrDefault(id));
    }

    public async Task<List<PlanVersionDto>?> VersionsAsync(int id)
    {
        if (!await db.ReadingPlans.AnyAsync(p => p.PlanId == id)) return null;
        return (await db.ReadingPlanVersions.AsNoTracking().Where(v => v.PlanId == id)
            .Include(v => v.PlanVersionStories).OrderByDescending(v => v.VersionNumber).ToListAsync())
            .Select(Map).ToList();
    }

    private async Task ValidateStoriesAsync(CreatePlanVersionDto dto)
    {
        if (dto.StoryIds.Count > 0 && await db.Stories.CountAsync(s => dto.StoryIds.Contains(s.StoryId)) != dto.StoryIds.Count)
            throw new ArgumentException("Danh sách có truyện không tồn tại.");
    }

    private static ReadingPlanVersion Version(CreatePlanVersionDto dto, int number) => new()
    {
        VersionNumber = number, Price = dto.Price, DurationMonths = dto.DurationMonths,
        ScopeType = dto.ScopeType, CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow,
        PlanVersionStories = dto.StoryIds.Select(id => new PlanVersionStory { StoryId = id }).ToList()
    };

    public async Task<ReadingPlanDto> CreateAsync(CreateReadingPlanDto dto)
    {
        await ValidateStoriesAsync(dto);
        var plan = new ReadingPlan { Name = dto.Name.Trim(), IsAvailable = true };
        plan.Versions.Add(Version(dto, 1));
        db.ReadingPlans.Add(plan);
        // EF saves the plan, version and story links in one transaction.
        await db.SaveChangesAsync();
        return (await GetAsync(plan.PlanId, true))!;
    }

    public async Task<PlanVersionDto?> AddVersionAsync(int id, CreatePlanVersionDto dto)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        // Serialize version allocation for this plan across requests/API instances.
        var plans = await db.ReadingPlans.FromSqlInterpolated($"SELECT * FROM `ReadingPlans` WHERE `PlanId` = {id} FOR UPDATE").ToListAsync();
        if (plans.Count == 0) return null;
        await ValidateStoriesAsync(dto);
        var number = (await db.ReadingPlanVersions.Where(v => v.PlanId == id)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0) + 1;
        var version = Version(dto, number);
        version.PlanId = id;
        db.ReadingPlanVersions.Add(version);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Map(version);
    }

    public async Task<bool> RenameAsync(int id, RenameReadingPlanDto dto)
    {
        return await db.ReadingPlans.Where(p => p.PlanId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, dto.Name.Trim())) > 0;
    }

    public async Task<bool> SetAvailabilityAsync(int id, bool available)
    {
        return await db.ReadingPlans.Where(p => p.PlanId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsAvailable, available)) > 0;
    }
}

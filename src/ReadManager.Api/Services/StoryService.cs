using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

public interface IStoryService
{
    Task<PagedResultDto<StoryListItemDto>> GetListAsync(StoryListQueryDto query);
    Task<StoryDetailDto?> GetPublicByIdAsync(int id);
    Task<StoryDetailDto?> GetByIdForAdminAsync(int id);
    Task<StoryDetailDto> CreateAsync(CreateStoryDto dto);
    Task<StoryDetailDto?> UpdateAsync(int id, UpdateStoryDto dto);
}

public class StoryService : IStoryService
{
    private static readonly string[] AllowedStatus = { "Ongoing", "Completed", "Paused" };
    private static readonly string[] AllowedVisibility = { "Draft", "Public", "Hidden" };
    private static readonly string[] AllowedAccess = { "Free", "Mixed", "Paid" };

    private readonly AppDbContext _db;

    public StoryService(AppDbContext db)
    {
        _db = db;
    }

    // ----- GET /api/stories : danh sách cho trang chủ / trang khám phá -----
    public async Task<PagedResultDto<StoryListItemDto>> GetListAsync(StoryListQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 12 : query.PageSize;

        // Chỉ hiện truyện Visibility = Public: đây là API phục vụ trang chủ/độc giả,
        // không phải API cho Admin xem tất cả (kể cả bản nháp).
        IQueryable<Story> stories = _db.Stories.Where(s => s.Visibility == "Public");

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            stories = stories.Where(s => s.Title.Contains(q) || s.AuthorName.Contains(q));
        }

        if (query.GenreId.HasValue)
        {
            stories = stories.Where(s =>
                _db.StoryGenres.Any(sg => sg.StoryId == s.StoryId && sg.GenreId == query.GenreId));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && AllowedStatus.Contains(query.Status))
            stories = stories.Where(s => s.PublicationStatus == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Access) && AllowedAccess.Contains(query.Access))
            stories = stories.Where(s => s.AccessPolicy == query.Access);

        stories = query.Sort switch
        {
            "newest" => stories.OrderByDescending(s => s.CreatedAt),
            "title" => stories.OrderBy(s => s.Title),
            _ => stories.OrderByDescending(s => s.UpdatedAt) // "updated" = mặc định
        };

        var totalCount = await stories.CountAsync();

        var pageStoryIds = await stories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => s.StoryId)
            .ToListAsync();

        var items = await BuildListItemsAsync(pageStoryIds);

        var ordered = pageStoryIds
            .Select(id => items.First(i => i.StoryId == id))
            .ToList();

        return new PagedResultDto<StoryListItemDto>
        {
            Items = ordered,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // Gộp chapter count + genres cho nhiều truyện cùng lúc bằng GroupBy,
    // tránh N+1 query (nếu làm foreach từng truyện rồi query riêng sẽ rất chậm).
    private async Task<List<StoryListItemDto>> BuildListItemsAsync(List<int> storyIds)
    {
        if (storyIds.Count == 0)
            return new List<StoryListItemDto>();

        var stories = await _db.Stories
            .Where(s => storyIds.Contains(s.StoryId))
            .ToListAsync();

        var chapterCounts = await _db.Chapters
            .Where(c => storyIds.Contains(c.StoryId) && c.PublicationStatus == "Published")
            .GroupBy(c => c.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        var genreRows = await _db.StoryGenres
            .Where(sg => storyIds.Contains(sg.StoryId))
            .Include(sg => sg.Genre)
            .ToListAsync();

        return stories.Select(s => new StoryListItemDto
        {
            StoryId = s.StoryId,
            Title = s.Title,
            Slug = s.Slug,
            AuthorName = s.AuthorName,
            CoverUrl = s.CoverUrl,
            PublicationStatus = s.PublicationStatus,
            AccessPolicy = s.AccessPolicy,
            CurrentPrice = s.CurrentPrice,
            PublishedChapterCount = chapterCounts.GetValueOrDefault(s.StoryId),
            Genres = genreRows.Where(sg => sg.StoryId == s.StoryId).Select(sg => sg.Genre.Name).ToList(),
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }

    // ----- GET /api/stories/{id} : chi tiết truyện cho ĐỘC GIẢ (PB11) -----
    // Chỉ trả về khi Visibility=Public — khớp đúng nguyên tắc đang áp dụng ở GetListAsync.
    // Story tồn tại nhưng KHÔNG Public cũng trả null (giống hệt "không tồn tại"), để không
    // lộ tín hiệu phân biệt "có bản Draft ở id này" cho người ngoài dò theo id tuần tự.
    public async Task<StoryDetailDto?> GetPublicByIdAsync(int id)
    {
        var detail = await GetByIdForAdminAsync(id);
        if (detail is null || detail.Visibility != "Public")
            return null;

        return detail;
    }

    // ----- Chi tiết truyện KHÔNG lọc Visibility — dùng cho form sửa của Admin, và cho
    // CreateAsync/UpdateAsync tự gọi lại để trả full detail ngay sau khi ghi DB (lúc đó
    // truyện có thể vẫn đang Draft, nên không được lọc ở đây). -----
    // TODO(auth): gắn [Authorize(Roles = "Admin")] ở controller khi PB02/PB03 xong —
    // hiện CHƯA có gì chặn, ai gọi route /admin cũng xem được mọi truyện kể cả Draft/Hidden.
    public async Task<StoryDetailDto?> GetByIdForAdminAsync(int id)
    {
        var story = await _db.Stories.FindAsync(id);
        if (story is null)
            return null;

        var publishedCount = await _db.Chapters
            .CountAsync(c => c.StoryId == id && c.PublicationStatus == "Published");

        var freeCount = await _db.Chapters
            .CountAsync(c => c.StoryId == id && c.PublicationStatus == "Published" && c.AccessLevel == "Free");

        var genres = await _db.StoryGenres
            .Where(sg => sg.StoryId == id)
            .Include(sg => sg.Genre)
            .Select(sg => new GenreRefDto(sg.Genre.GenreId, sg.Genre.Name))
            .ToListAsync();

        return MapToDetail(story, publishedCount, freeCount, genres);
    }

    // ----- POST /api/stories : tạo truyện mới -----
    public async Task<StoryDetailDto> CreateAsync(CreateStoryDto dto)
    {
        ValidateAccessAndVisibility(dto.AccessPolicy, dto.Visibility);
        ValidatePrice(dto.AccessPolicy, dto.CurrentPrice);

        if (!await _db.Users.AnyAsync(u => u.UserId == dto.CreatedBy))
            throw new InvalidOperationException("CreatedBy không khớp người dùng nào trong hệ thống.");

        var title = dto.Title.Trim();

        var story = new Story
        {
            Title = title,
            Slug = await BuildUniqueSlugAsync(title),
            AuthorName = dto.AuthorName.Trim(),
            Synopsis = dto.Synopsis.Trim(),
            CoverUrl = string.IsNullOrWhiteSpace(dto.CoverUrl) ? null : dto.CoverUrl.Trim(),
            PublicationStatus = "Ongoing",
            Visibility = dto.Visibility,
            AccessPolicy = dto.AccessPolicy,
            CurrentPrice = dto.AccessPolicy == "Free" ? null : dto.CurrentPrice,
            CreatedBy = dto.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstPublishedAt = dto.Visibility == "Public" ? DateTime.UtcNow : null
        };

        // Cần transaction vì phải SaveChanges 2 lần: lần 1 để MySQL sinh StoryId (AUTO_INCREMENT),
        // lần 2 để lưu StoryGenres tham chiếu StoryId đó. Rollback cả hai nếu bước sau lỗi.
        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Stories.Add(story);
        await _db.SaveChangesAsync();

        await SyncGenresAsync(story.StoryId, dto.GenreIds);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        return (await GetByIdForAdminAsync(story.StoryId))!;
    }

    // ----- PUT /api/stories/{id} : sửa truyện + đổi trạng thái phát hành + gán lại thể loại -----
    public async Task<StoryDetailDto?> UpdateAsync(int id, UpdateStoryDto dto)
    {
        var story = await _db.Stories.FindAsync(id);
        if (story is null)
            return null;

        ValidateStatusVisibilityAccess(dto.PublicationStatus, dto.Visibility, dto.AccessPolicy);
        ValidatePrice(dto.AccessPolicy, dto.CurrentPrice);

        var wasPublic = story.Visibility == "Public";

        story.Title = dto.Title.Trim();
        story.AuthorName = dto.AuthorName.Trim();
        story.Synopsis = dto.Synopsis.Trim();
        story.CoverUrl = string.IsNullOrWhiteSpace(dto.CoverUrl) ? null : dto.CoverUrl.Trim();
        story.PublicationStatus = dto.PublicationStatus;
        story.Visibility = dto.Visibility;
        story.AccessPolicy = dto.AccessPolicy;
        story.CurrentPrice = dto.AccessPolicy == "Free" ? null : dto.CurrentPrice;
        story.UpdatedAt = DateTime.UtcNow;

        // Ghi nhận mốc "lần đầu công khai" — chỉ set 1 lần, không ghi đè các lần Public lại sau đó.
        if (!wasPublic && dto.Visibility == "Public" && story.FirstPublishedAt is null)
            story.FirstPublishedAt = DateTime.UtcNow;

        // Story đã tồn tại (đã có StoryId) nên không cần 2 lần SaveChanges/transaction
        // như CreateAsync — 1 lần SaveChangesAsync là đủ, EF Core tự gộp thành 1 transaction ngầm.
        await SyncGenresAsync(id, dto.GenreIds);
        await _db.SaveChangesAsync();

        return await GetByIdForAdminAsync(id);
    }

    // So sánh danh sách GenreId mới với StoryGenres hiện có trong DB rồi Add/Remove phần chênh lệch.
    // Chỉ thao tác trên ChangeTracker, KHÔNG gọi SaveChangesAsync — để caller tự quyết định lúc lưu.
    private async Task SyncGenresAsync(int storyId, List<int> genreIds)
    {
        var distinctIds = genreIds.Distinct().ToList();

        if (distinctIds.Count > 0)
        {
            var validCount = await _db.Genres.CountAsync(g => distinctIds.Contains(g.GenreId));
            if (validCount != distinctIds.Count)
                throw new InvalidOperationException("Danh sách gán có GenreId không tồn tại.");
        }

        var current = await _db.StoryGenres.Where(sg => sg.StoryId == storyId).ToListAsync();

        var toRemove = current.Where(c => !distinctIds.Contains(c.GenreId));
        _db.StoryGenres.RemoveRange(toRemove);

        var toAdd = distinctIds.Where(gid => current.All(c => c.GenreId != gid));
        foreach (var genreId in toAdd)
            _db.StoryGenres.Add(new StoryGenre { StoryId = storyId, GenreId = genreId });
    }

    private static void ValidateAccessAndVisibility(string access, string visibility)
    {
        if (!AllowedAccess.Contains(access))
            throw new InvalidOperationException($"AccessPolicy \"{access}\" không hợp lệ.");
        if (!AllowedVisibility.Contains(visibility))
            throw new InvalidOperationException($"Visibility \"{visibility}\" không hợp lệ.");
    }

    private static void ValidateStatusVisibilityAccess(string status, string visibility, string access)
    {
        if (!AllowedStatus.Contains(status))
            throw new InvalidOperationException($"PublicationStatus \"{status}\" không hợp lệ.");
        ValidateAccessAndVisibility(access, visibility);
    }

    private static void ValidatePrice(string access, decimal? price)
    {
        // Khớp CHECK constraint CK_Stories_CurrentPrice ở AppDbContext: NULL hoặc > 0.
        // Kiểm tra sớm ở đây để trả lỗi 400 dễ hiểu, thay vì để MySQL ném exception khó đọc.
        if (access != "Free" && (price is null || price <= 0))
            throw new InvalidOperationException("Truyện Mixed/Paid cần nhập giá lớn hơn 0.");
    }

    private static StoryDetailDto MapToDetail(
        Story s, int publishedCount, int freeCount, List<GenreRefDto> genres) => new()
        {
            StoryId = s.StoryId,
            Title = s.Title,
            Slug = s.Slug,
            AuthorName = s.AuthorName,
            Synopsis = s.Synopsis,
            CoverUrl = s.CoverUrl,
            PublicationStatus = s.PublicationStatus,
            Visibility = s.Visibility,
            AccessPolicy = s.AccessPolicy,
            CurrentPrice = s.CurrentPrice,
            CreatedBy = s.CreatedBy,
            PublishedChapterCount = publishedCount,
            FreeChapterCount = freeCount,
            Genres = genres,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            FirstPublishedAt = s.FirstPublishedAt
        };

    private async Task<string> BuildUniqueSlugAsync(string title)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        var suffix = 2;

        while (await _db.Stories.AnyAsync(s => s.Slug == slug))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    private static string Slugify(string value)
    {
        var lower = value.ToLowerInvariant().Replace('đ', 'd').Replace('Đ', 'd');
        var normalized = lower.Normalize(NormalizationForm.FormD);

        var stripped = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped.Append(c);
        }

        var slug = Regex.Replace(stripped.ToString(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "truyen" : slug;
    }
}
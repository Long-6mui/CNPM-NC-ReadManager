using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Statistics.Content;

namespace ReadManager.Api.Services;

// BACKEND 3 — PB40: THỐNG KÊ MỨC ĐỘ QUAN TÂM NỘI DUNG
// Gom 4 nguồn dữ liệu theo khoảng thời gian:
//   Lượt đọc   ← bảng ChapterReads   (ghi trong ChapterService khi độc giả đọc chương)
//   Theo dõi   ← bảng StoryFollows   (PB31)
//   Lượt mua   ← bảng Orders         (đơn mua truyện đã Paid, tính theo PaidAt)
//   Bình luận  ← bảng Comments       (PB32, chỉ bình luận đang hiện)
public interface IContentStatisticsService
{
    // Ném ArgumentException (→ 400) nếu khoảng ngày sai
    Task<ContentOverviewDto> GetOverviewAsync(ContentStatsQuery query);
    Task<List<StoryContentStatsDto>> GetStoriesAsync(ContentStatsQuery query);
    Task<List<ChapterContentStatsDto>?> GetChaptersAsync(int storyId, ContentStatsQuery query);   // null = không có truyện
    Task<List<GenreContentStatsDto>> GetGenresAsync(ContentStatsQuery query);
    Task<List<DailyContentStatsDto>> GetDailyAsync(ContentStatsQuery query);
}

public class ContentStatisticsService : IContentStatisticsService
{
    // Trọng số điểm quan tâm (đổi ở đây là đổi cho toàn bộ thống kê)
    public const int ReadWeight = 1;
    public const int FollowWeight = 5;
    public const int CommentWeight = 3;
    public const int PurchaseWeight = 10;

    private const int DefaultDays = 30;
    private const int MaxDays = 366;
    private const string Visible = "Visible";
    private const string Paid = "Paid";
    private const string TypeStory = "Story";

    // Giờ Việt Nam (UTC+7) — Windows: "SE Asia Standard Time", Linux/Mac: "Asia/Ho_Chi_Minh"
    private static readonly TimeZoneInfo VietnamTime = FindVietnamTimeZone();

    private readonly AppDbContext _db;

    public ContentStatisticsService(AppDbContext db)
    {
        _db = db;
    }

    // =====================================================================
    // TỔNG QUAN
    // =====================================================================
    public async Task<ContentOverviewDto> GetOverviewAsync(ContentStatsQuery query)
    {
        var range = ResolveRange(query);
        var stories = await BuildStoryStatsAsync(range);

        // Độc giả khác nhau trên TOÀN hệ thống (1 người đọc nhiều truyện chỉ tính 1)
        var uniqueReaders = await _db.ChapterReads
            .Where(r => r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc && r.UserId != null)
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync();

        return new ContentOverviewDto
        {
            From = range.From,
            To = range.To,
            Reads = stories.Sum(s => s.Reads),
            UniqueReaders = uniqueReaders,
            NewFollows = stories.Sum(s => s.NewFollows),
            Purchases = stories.Sum(s => s.Purchases),
            Comments = stories.Sum(s => s.Comments),
            TopStories = stories
                .Where(s => s.InterestScore > 0)
                .OrderByDescending(s => s.InterestScore).ThenBy(s => s.Title)
                .Take(5).ToList(),
            TopGenres = (await BuildGenreStatsAsync(stories))
                .Where(g => g.InterestScore > 0)
                .OrderByDescending(g => g.InterestScore).ThenBy(g => g.Name)
                .Take(5).ToList()
        };
    }

    // =====================================================================
    // THEO TRUYỆN (xếp hạng)
    // =====================================================================
    public async Task<List<StoryContentStatsDto>> GetStoriesAsync(ContentStatsQuery query)
    {
        var range = ResolveRange(query);
        var top = Math.Clamp(query.Top, 1, 100);
        var stories = await BuildStoryStatsAsync(range);

        IOrderedEnumerable<StoryContentStatsDto> sorted = (query.Sort ?? "interest").ToLowerInvariant() switch
        {
            "reads" => stories.OrderByDescending(s => s.Reads),
            "follows" => stories.OrderByDescending(s => s.NewFollows),
            "purchases" => stories.OrderByDescending(s => s.Purchases),
            "comments" => stories.OrderByDescending(s => s.Comments),
            "interest" => stories.OrderByDescending(s => s.InterestScore),
            _ => throw new ArgumentException("sort phải là interest, reads, follows, purchases hoặc comments.")
        };

        return sorted.ThenBy(s => s.Title).Take(top).ToList();
    }

    // =====================================================================
    // THEO CHƯƠNG của 1 truyện
    // =====================================================================
    public async Task<List<ChapterContentStatsDto>?> GetChaptersAsync(int storyId, ContentStatsQuery query)
    {
        var range = ResolveRange(query);
        if (!await _db.Stories.AnyAsync(s => s.StoryId == storyId))
            return null;

        var reads = await _db.ChapterReads
            .Where(r => r.StoryId == storyId && r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .GroupBy(r => r.ChapterId)
            .Select(g => new { ChapterId = g.Key, Reads = g.Count(), Unique = g.Where(x => x.UserId != null).Select(x => x.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.ChapterId);

        var comments = await _db.Comments
            .Where(c => c.StoryId == storyId && c.ChapterId != null && c.Status == Visible
                     && c.CreatedAt >= range.FromUtc && c.CreatedAt < range.ToUtc)
            .GroupBy(c => c.ChapterId!.Value)
            .Select(g => new { ChapterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ChapterId, x => x.Count);

        var chapters = await _db.Chapters.AsNoTracking()
            .Where(c => c.StoryId == storyId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => new { c.ChapterId, c.ChapterNumber, c.Title, c.AccessLevel })
            .ToListAsync();

        return chapters.Select(c => new ChapterContentStatsDto
        {
            ChapterId = c.ChapterId,
            ChapterNumber = c.ChapterNumber,
            Title = c.Title,
            AccessLevel = c.AccessLevel,
            Reads = reads.TryGetValue(c.ChapterId, out var r) ? r.Reads : 0,
            UniqueReaders = reads.TryGetValue(c.ChapterId, out var r2) ? r2.Unique : 0,
            Comments = comments.GetValueOrDefault(c.ChapterId)
        }).ToList();
    }

    // =====================================================================
    // THEO THỂ LOẠI
    // =====================================================================
    public async Task<List<GenreContentStatsDto>> GetGenresAsync(ContentStatsQuery query)
    {
        var range = ResolveRange(query);
        var stories = await BuildStoryStatsAsync(range);
        return (await BuildGenreStatsAsync(stories))
            .OrderByDescending(g => g.InterestScore).ThenBy(g => g.Name)
            .ToList();
    }

    // =====================================================================
    // THEO TỪNG NGÀY (biểu đồ)
    // =====================================================================
    public async Task<List<DailyContentStatsDto>> GetDailyAsync(ContentStatsQuery query)
    {
        var range = ResolveRange(query);

        // Lấy mốc thời gian rồi chia theo NGÀY GIỜ VIỆT NAM (làm trong bộ nhớ cho đúng múi giờ)
        var reads = await _db.ChapterReads
            .Where(r => r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .Select(r => r.ReadAt).ToListAsync();
        var follows = await _db.StoryFollows
            .Where(f => f.CreatedAt >= range.FromUtc && f.CreatedAt < range.ToUtc)
            .Select(f => f.CreatedAt).ToListAsync();
        var purchases = await PaidStoryOrders(range)
            .Select(o => o.PaidAt!.Value).ToListAsync();
        var comments = await _db.Comments
            .Where(c => c.Status == Visible && c.CreatedAt >= range.FromUtc && c.CreatedAt < range.ToUtc)
            .Select(c => c.CreatedAt).ToListAsync();

        var readsByDay = CountByDay(reads);
        var followsByDay = CountByDay(follows);
        var purchasesByDay = CountByDay(purchases);
        var commentsByDay = CountByDay(comments);

        // Trả đủ mọi ngày trong khoảng (ngày không có gì → 0) để biểu đồ không bị đứt
        var result = new List<DailyContentStatsDto>();
        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            result.Add(new DailyContentStatsDto
            {
                Date = day,
                Reads = readsByDay.GetValueOrDefault(day),
                NewFollows = followsByDay.GetValueOrDefault(day),
                Purchases = purchasesByDay.GetValueOrDefault(day),
                Comments = commentsByDay.GetValueOrDefault(day)
            });
        }
        return result;
    }

    // =====================================================================
    // HÀM PHỤ
    // =====================================================================

    // Số liệu của TẤT CẢ truyện trong khoảng thời gian (mỗi nguồn 1 câu truy vấn GROUP BY)
    private async Task<List<StoryContentStatsDto>> BuildStoryStatsAsync(DateRange range)
    {
        var reads = await _db.ChapterReads
            .Where(r => r.ReadAt >= range.FromUtc && r.ReadAt < range.ToUtc)
            .GroupBy(r => r.StoryId)
            .Select(g => new { StoryId = g.Key, Reads = g.Count(), Unique = g.Where(x => x.UserId != null).Select(x => x.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.StoryId);

        var newFollows = await _db.StoryFollows
            .Where(f => f.CreatedAt >= range.FromUtc && f.CreatedAt < range.ToUtc)
            .GroupBy(f => f.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        var totalFollowers = await _db.StoryFollows
            .GroupBy(f => f.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        var purchases = await PaidStoryOrders(range)
            .GroupBy(o => o.StoryId!.Value)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        var comments = await _db.Comments
            .Where(c => c.Status == Visible && c.CreatedAt >= range.FromUtc && c.CreatedAt < range.ToUtc)
            .GroupBy(c => c.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        var stories = await _db.Stories.AsNoTracking()
            .Select(s => new { s.StoryId, s.Title, s.Visibility })
            .ToListAsync();

        return stories.Select(s =>
        {
            var dto = new StoryContentStatsDto
            {
                StoryId = s.StoryId,
                Title = s.Title,
                Visibility = s.Visibility,
                Reads = reads.TryGetValue(s.StoryId, out var r) ? r.Reads : 0,
                UniqueReaders = reads.TryGetValue(s.StoryId, out var r2) ? r2.Unique : 0,
                NewFollows = newFollows.GetValueOrDefault(s.StoryId),
                TotalFollowers = totalFollowers.GetValueOrDefault(s.StoryId),
                Purchases = purchases.GetValueOrDefault(s.StoryId),
                Comments = comments.GetValueOrDefault(s.StoryId)
            };
            dto.InterestScore = Score(dto.Reads, dto.NewFollows, dto.Comments, dto.Purchases);
            return dto;
        }).ToList();
    }

    // Cộng số liệu truyện theo thể loại
    private async Task<List<GenreContentStatsDto>> BuildGenreStatsAsync(List<StoryContentStatsDto> stories)
    {
        var byStory = stories.ToDictionary(s => s.StoryId);
        var genres = await _db.Genres.AsNoTracking()
            .Select(g => new { g.GenreId, g.Name })
            .ToListAsync();
        var links = await _db.StoryGenres.AsNoTracking()
            .Select(sg => new { sg.GenreId, sg.StoryId })
            .ToListAsync();
        var storiesOfGenre = links.ToLookup(l => l.GenreId, l => l.StoryId);

        return genres.Select(g =>
        {
            var list = storiesOfGenre[g.GenreId]
                .Where(byStory.ContainsKey)
                .Select(id => byStory[id])
                .ToList();
            var dto = new GenreContentStatsDto
            {
                GenreId = g.GenreId,
                Name = g.Name,
                StoryCount = list.Count,
                Reads = list.Sum(s => s.Reads),
                NewFollows = list.Sum(s => s.NewFollows),
                Purchases = list.Sum(s => s.Purchases),
                Comments = list.Sum(s => s.Comments)
            };
            dto.InterestScore = Score(dto.Reads, dto.NewFollows, dto.Comments, dto.Purchases);
            return dto;
        }).ToList();
    }

    // Đơn mua TRỌN BỘ TRUYỆN đã thanh toán trong khoảng (tính theo lúc thanh toán)
    private IQueryable<Entities.Order> PaidStoryOrders(DateRange range)
        => _db.Orders.Where(o => o.OrderType == TypeStory && o.Status == Paid && o.StoryId != null
                              && o.PaidAt != null && o.PaidAt >= range.FromUtc && o.PaidAt < range.ToUtc);

    private static int Score(int reads, int follows, int comments, int purchases)
        => reads * ReadWeight + follows * FollowWeight + comments * CommentWeight + purchases * PurchaseWeight;

    private static Dictionary<DateOnly, int> CountByDay(IEnumerable<DateTime> utcTimes)
        => utcTimes
            .GroupBy(t => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), VietnamTime)))
            .ToDictionary(g => g.Key, g => g.Count());

    // Khoảng ngày (giờ VN) → khoảng giờ UTC [FromUtc, ToUtc) để so sánh với dữ liệu lưu UTC
    private static DateRange ResolveRange(ContentStatsQuery query)
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
}

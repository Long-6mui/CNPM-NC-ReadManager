using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Discovery;
using ReadManager.Api.DTOs.Stories;

namespace ReadManager.Api.Services;

// BACKEND 3 — PB KHÁM PHÁ & BẢNG XẾP HẠNG
// Bảng xếp hạng truyện theo lượt đọc / lượt theo dõi
// Xu hướng & từ khóa tìm kiếm nổi bật
// Gợi ý truyện cho người dùng
public interface IDiscoveryService
{
    Task<List<StoryRankingItemDto>> GetRankingAsync(RankingQueryDto query);
    Task<TrendingKeywordsDto> GetTrendingAsync();
    Task<List<StoryListItemDto>> GetRecommendedAsync(int? userId, int limit = 10);
}

public class DiscoveryService : IDiscoveryService
{
    private const string PublicVisibility = "Public";
    private const string Published = "Published";

    private readonly AppDbContext _db;

    public DiscoveryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<StoryRankingItemDto>> GetRankingAsync(RankingQueryDto query)
    {
        var top = Math.Clamp(query.Top, 1, 50);
        var now = DateTime.UtcNow;

        var fromTime = (query.Period?.ToLowerInvariant()) switch
        {
            "day" => now.AddDays(-1),
            "month" => now.AddDays(-30),
            "all" => DateTime.MinValue,
            _ => now.AddDays(-7) // "week" = mặc định
        };

        var by = (query.By?.ToLowerInvariant()) switch
        {
            "follows" => "follows",
            "score" => "score",
            _ => "reads" // "reads" = mặc định
        };

        // Danh sách truyện công khai
        var storiesQuery = _db.Stories.AsNoTracking().Where(s => s.Visibility == PublicVisibility);
        if (query.GenreId.HasValue)
        {
            storiesQuery = storiesQuery.Where(s =>
                _db.StoryGenres.Any(sg => sg.StoryId == s.StoryId && sg.GenreId == query.GenreId.Value));
        }

        var publicStoryIds = await storiesQuery.Select(s => s.StoryId).ToListAsync();
        if (publicStoryIds.Count == 0)
            return new List<StoryRankingItemDto>();

        // Thống kê lượt đọc
        var readsQuery = _db.ChapterReads.AsNoTracking()
            .Where(r => publicStoryIds.Contains(r.StoryId));
        if (fromTime > DateTime.MinValue)
            readsQuery = readsQuery.Where(r => r.ReadAt >= fromTime);

        var readsByStory = await readsQuery
            .GroupBy(r => r.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        // Thống kê lượt theo dõi
        var followsQuery = _db.StoryFollows.AsNoTracking()
            .Where(f => publicStoryIds.Contains(f.StoryId));
        if (fromTime > DateTime.MinValue)
            followsQuery = followsQuery.Where(f => f.CreatedAt >= fromTime);

        var followsByStory = await followsQuery
            .GroupBy(f => f.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        // Ghép và sắp xếp
        var rankedIds = publicStoryIds
            .Select(id =>
            {
                var reads = readsByStory.GetValueOrDefault(id);
                var follows = followsByStory.GetValueOrDefault(id);
                var metric = by switch
                {
                    "follows" => follows,
                    "score" => reads + follows * 5,
                    _ => reads
                };
                return new { StoryId = id, Metric = metric, Reads = reads, Follows = follows };
            })
            .OrderByDescending(x => x.Metric)
            .ThenByDescending(x => x.Reads)
            .ThenByDescending(x => x.Follows)
            .Take(top)
            .ToList();

        if (rankedIds.Count == 0)
            return new List<StoryRankingItemDto>();

        var selectedIds = rankedIds.Select(x => x.StoryId).ToList();

        var stories = await _db.Stories.AsNoTracking()
            .Where(s => selectedIds.Contains(s.StoryId))
            .ToDictionaryAsync(s => s.StoryId);

        var chapterCounts = await _db.Chapters.AsNoTracking()
            .Where(c => selectedIds.Contains(c.StoryId) && c.PublicationStatus == Published)
            .GroupBy(c => c.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        var genresByStory = await _db.StoryGenres.AsNoTracking()
            .Where(sg => selectedIds.Contains(sg.StoryId))
            .OrderBy(sg => sg.Genre.Name)
            .Select(sg => new { sg.StoryId, sg.Genre.Name })
            .ToListAsync();

        var genreMap = genresByStory
            .GroupBy(x => x.StoryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

        var result = new List<StoryRankingItemDto>();
        for (int i = 0; i < rankedIds.Count; i++)
        {
            var item = rankedIds[i];
            if (!stories.TryGetValue(item.StoryId, out var story))
                continue;

            result.Add(new StoryRankingItemDto
            {
                Rank = i + 1,
                StoryId = story.StoryId,
                Title = story.Title,
                Slug = story.Slug,
                AuthorName = story.AuthorName,
                CoverUrl = story.CoverUrl,
                PublicationStatus = story.PublicationStatus,
                AccessPolicy = story.AccessPolicy,
                PublishedChapterCount = chapterCounts.GetValueOrDefault(story.StoryId),
                Genres = genreMap.GetValueOrDefault(story.StoryId) ?? new List<string>(),
                MetricCount = item.Metric,
                TotalReads = item.Reads,
                TotalFollows = item.Follows
            });
        }

        return result;
    }

    public async Task<TrendingKeywordsDto> GetTrendingAsync()
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        // Top truyện đọc nhiều trong 7 ngày qua
        var topReadStoryIds = await _db.ChapterReads.AsNoTracking()
            .Where(r => r.ReadAt >= weekAgo)
            .GroupBy(r => r.StoryId)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToListAsync();

        var stories = await _db.Stories.AsNoTracking()
            .Where(s => topReadStoryIds.Contains(s.StoryId) && s.Visibility == PublicVisibility)
            .ToListAsync();

        // Tạo danh sách từ khóa nổi bật từ tên truyện và tên tác giả hot
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var story in stories)
        {
            if (!string.IsNullOrWhiteSpace(story.Title))
                keywords.Add(story.Title);
            if (!string.IsNullOrWhiteSpace(story.AuthorName))
                keywords.Add(story.AuthorName);
        }

        // Top thể loại hot
        var hotGenres = await _db.StoryGenres.AsNoTracking()
            .Where(sg => topReadStoryIds.Contains(sg.StoryId))
            .GroupBy(sg => sg.Genre.Name)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToListAsync();

        foreach (var g in hotGenres)
            keywords.Add(g);

        // Featured stories
        var featuredRanking = await GetRankingAsync(new RankingQueryDto { By = "reads", Period = "week", Top = 5 });
        var featuredStories = featuredRanking.Select(r => new StoryListItemDto
        {
            StoryId = r.StoryId,
            Title = r.Title,
            Slug = r.Slug,
            AuthorName = r.AuthorName,
            CoverUrl = r.CoverUrl,
            PublicationStatus = r.PublicationStatus,
            AccessPolicy = r.AccessPolicy,
            PublishedChapterCount = r.PublishedChapterCount,
            Genres = r.Genres
        }).ToList();

        return new TrendingKeywordsDto
        {
            Keywords = keywords.Take(10).ToList(),
            HotGenres = hotGenres,
            FeaturedStories = featuredStories
        };
    }

    public async Task<List<StoryListItemDto>> GetRecommendedAsync(int? userId, int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 30);

        if (userId.HasValue)
        {
            // Lấy 30 lượt đọc gần nhất của người dùng
            var recentStoryIds = await _db.ChapterReads.AsNoTracking()
                .Where(r => r.UserId == userId.Value)
                .OrderByDescending(r => r.ReadAt)
                .Select(r => r.StoryId)
                .Distinct()
                .Take(10)
                .ToListAsync();

            if (recentStoryIds.Count > 0)
            {
                // Lấy các thể loại ưa thích
                var preferredGenreIds = await _db.StoryGenres.AsNoTracking()
                    .Where(sg => recentStoryIds.Contains(sg.StoryId))
                    .GroupBy(sg => sg.GenreId)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToListAsync();

                if (preferredGenreIds.Count > 0)
                {
                    // Lấy các truyện công khai cùng thể loại mà người dùng chưa đọc nhiều
                    var recommendedIds = await _db.StoryGenres.AsNoTracking()
                        .Where(sg => preferredGenreIds.Contains(sg.GenreId)
                                  && !recentStoryIds.Contains(sg.StoryId)
                                  && sg.Story.Visibility == PublicVisibility)
                        .Select(sg => sg.StoryId)
                        .Distinct()
                        .Take(limit)
                        .ToListAsync();

                    if (recommendedIds.Count > 0)
                    {
                        var recStories = await BuildStoryListItemsAsync(recommendedIds);
                        if (recStories.Count >= limit)
                            return recStories.Take(limit).ToList();
                    }
                }
            }
        }

        // Khách hoặc chưa đủ truyện gợi ý cá nhân → lấy top truyện theo tuần
        var fallbackRanking = await GetRankingAsync(new RankingQueryDto { By = "reads", Period = "week", Top = limit });
        return fallbackRanking.Select(r => new StoryListItemDto
        {
            StoryId = r.StoryId,
            Title = r.Title,
            Slug = r.Slug,
            AuthorName = r.AuthorName,
            CoverUrl = r.CoverUrl,
            PublicationStatus = r.PublicationStatus,
            AccessPolicy = r.AccessPolicy,
            PublishedChapterCount = r.PublishedChapterCount,
            Genres = r.Genres
        }).ToList();
    }

    private async Task<List<StoryListItemDto>> BuildStoryListItemsAsync(List<int> storyIds)
    {
        var stories = await _db.Stories.AsNoTracking()
            .Where(s => storyIds.Contains(s.StoryId) && s.Visibility == PublicVisibility)
            .ToListAsync();

        var chapterCounts = await _db.Chapters.AsNoTracking()
            .Where(c => storyIds.Contains(c.StoryId) && c.PublicationStatus == Published)
            .GroupBy(c => c.StoryId)
            .Select(g => new { StoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoryId, x => x.Count);

        var genresByStory = await _db.StoryGenres.AsNoTracking()
            .Where(sg => storyIds.Contains(sg.StoryId))
            .OrderBy(sg => sg.Genre.Name)
            .Select(sg => new { sg.StoryId, sg.Genre.Name })
            .ToListAsync();

        var genreMap = genresByStory
            .GroupBy(x => x.StoryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

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
            Genres = genreMap.GetValueOrDefault(s.StoryId) ?? new List<string>(),
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }
}

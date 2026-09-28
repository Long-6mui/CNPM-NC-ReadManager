using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services.Mocks;

public class MockStoryService : IStoryService
{
    private static readonly List<Genre> SampleGenres = new()
    {
        new Genre { GenreId = 1, Name = "Ngôn tình", Slug = "ngon-tinh" },
        new Genre { GenreId = 2, Name = "Tiên hiệp", Slug = "tien-hiep" },
        new Genre { GenreId = 3, Name = "Trinh thám", Slug = "trinh-tham" },
    };

    private static readonly List<StorySummary> SampleStories = Enumerable.Range(1, 8).Select(i => new StorySummary
    {
        StoryId = i,
        Title = $"Truyện demo số {i}",
        Slug = $"truyen-demo-so-{i}",
        AuthorName = $"Tác giả {i}",
        CoverUrl = null,
        PublicationStatus = i % 2 == 0 ? "Completed" : "Ongoing",
        AccessPolicy = "Free",
        GenreNames = new List<string> { SampleGenres[i % SampleGenres.Count].Name }
    }).ToList();

    public Task<PagedResult<StorySummary>> GetStoriesAsync(int page = 1, string? search = null, int? genreId = null)
    {
        var items = SampleStories.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
            items = items.Where(s => s.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (genreId.HasValue)
            items = items.Where(s => s.StoryId % SampleGenres.Count == genreId.Value % SampleGenres.Count);

        var result = new PagedResult<StorySummary>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = 20,
            TotalItems = items.Count(),
            TotalPages = 1
        };
        return Task.FromResult(result);
    }

    public Task<StoryDetail> GetStoryDetailAsync(string slug)
    {
        var summary = SampleStories.FirstOrDefault(s => s.Slug == slug) ?? SampleStories[0];
        var detail = new StoryDetail
        {
            StoryId = summary.StoryId,
            Title = summary.Title,
            Slug = summary.Slug,
            AuthorName = summary.AuthorName,
            Synopsis = "Đây là nội dung giới thiệu demo, dùng để xem giao diện màn Chi tiết truyện trong lúc chờ API thật từ Backend.",
            CoverUrl = summary.CoverUrl,
            PublicationStatus = summary.PublicationStatus,
            AccessPolicy = summary.AccessPolicy,
            CurrentPrice = 0,
            FirstPublishedAt = DateTime.Now.AddMonths(-3),
            Genres = SampleGenres
        };
        return Task.FromResult(detail);
    }

    public Task<List<Genre>> GetGenresAsync() => Task.FromResult(SampleGenres);
}

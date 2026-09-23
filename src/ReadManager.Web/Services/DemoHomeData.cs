using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Services;

// Temporary UI data only. Replace this provider with API calls during integration.
public static class DemoHomeData
{
    public static HomeVm GetHome(string? query, int? genreId)
    {
        List<GenreVm> genres = [new(1, "Phiêu lưu"), new(2, "Đời thường"), new(3, "Kỳ ảo")];
        string[] titles = ["Chuyến tàu qua miền gió", "Tiệm sách cuối con đường", "Người giữ những vì sao", "Lá thư mùa hạ", "Hành trình phía chân trời", "Khu vườn trên mây"];
        var stories = titles.Select((title, i) => new StoryCardVm
        {
            Id = i + 1, Title = title, Author = "Tác giả mẫu " + (i + 1),
            GenreId = i % 3 + 1, PublishedChapterCount = (i + 1) * 5,
            Access = i == 2 ? AccessPolicy.Paid : i == 4 ? AccessPolicy.Mixed : AccessPolicy.Free,
            Status = i % 2 == 0 ? StoryStatus.Ongoing : StoryStatus.Completed,
            CreatedAtSort = new DateTime(2026, 9, 1).AddDays(i),
            UpdatedAtSort = new DateTime(2026, 9, 20).AddHours(6 - i)
        }).ToList();
        var filtered = stories.Where(s =>
            (!genreId.HasValue || s.GenreId == genreId) &&
            (string.IsNullOrWhiteSpace(query) || s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.Author.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        return new HomeVm
        {
            Updated = filtered.OrderByDescending(s => s.UpdatedAtSort).ToList(),
            Newest = filtered.OrderByDescending(s => s.CreatedAtSort).ToList(),
            Free = filtered.Where(s => s.Access == AccessPolicy.Free).ToList(),
            Genres = genres, TotalStories = stories.Count, TotalGenres = genres.Count,
            TotalChapters = stories.Sum(s => s.PublishedChapterCount), Query = query, GenreId = genreId
        };
    }
}
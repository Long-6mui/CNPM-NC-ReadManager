namespace ReadManager.Api.DTOs.Discovery;

public class StoryRankingItemDto
{
    public int Rank { get; set; }
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string PublicationStatus { get; set; } = string.Empty;
    public string AccessPolicy { get; set; } = string.Empty;
    public int PublishedChapterCount { get; set; }
    public List<string> Genres { get; set; } = new();

    // Thống kê tương ứng tiêu chí xếp hạng
    public int MetricCount { get; set; } // Số lượt đọc / số lượt theo dõi tùy theo By
    public int TotalReads { get; set; }
    public int TotalFollows { get; set; }
}

namespace ReadManager.Api.DTOs.Statistics.Content;

// PB40 — TỔNG QUAN mức độ quan tâm nội dung trong khoảng thời gian
public class ContentOverviewDto
{
    public DateOnly From { get; set; }        // ngày bắt đầu (giờ Việt Nam)
    public DateOnly To { get; set; }          // ngày kết thúc (tính cả ngày này)

    public int Reads { get; set; }
    public int UniqueReaders { get; set; }
    public int NewFollows { get; set; }
    public int Purchases { get; set; }
    public int Comments { get; set; }

    public List<StoryContentStatsDto> TopStories { get; set; } = new();   // 5 truyện điểm quan tâm cao nhất
    public List<GenreContentStatsDto> TopGenres { get; set; } = new();    // 5 thể loại điểm quan tâm cao nhất
}

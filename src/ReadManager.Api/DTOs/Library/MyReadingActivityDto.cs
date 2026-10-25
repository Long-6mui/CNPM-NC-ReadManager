namespace ReadManager.Api.DTOs.Library;

// Lịch sử "Đọc tiếp" (Continue Reading) cho từng bộ truyện mà độc giả đang đọc dở
public class ContinueReadingItemDto
{
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = string.Empty;
    public string StorySlug { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public int LastChapterId { get; set; }
    public int LastChapterNumber { get; set; }
    public string LastChapterTitle { get; set; } = string.Empty;
    public int TotalPublishedChapters { get; set; }
    public double ProgressPercent { get; set; }
    public DateTime LastReadAtUtc { get; set; }
}

// Thống kê hoạt động đọc cá nhân của độc giả (Reading Streak + Đọc tiếp + Thể loại yêu thích)
public class MyReadingActivityDto
{
    public int UserId { get; set; }
    public string MemberTier { get; set; } = string.Empty;
    public int CurrentStreakDays { get; set; }     // Chuỗi ngày đọc liên tiếp hiện tại
    public int LongestStreakDays { get; set; }     // Kỷ lục chuỗi ngày đọc liên tiếp dài nhất
    public int TotalChaptersRead { get; set; }     // Tổng số lượt đọc chương
    public int UniqueChaptersRead { get; set; }    // Số chương khác nhau đã đọc
    public int UniqueStoriesRead { get; set; }     // Số bộ truyện đã đọc
    public List<string> FavoriteGenres { get; set; } = new();
    public List<ContinueReadingItemDto> ContinueReading { get; set; } = new();
}

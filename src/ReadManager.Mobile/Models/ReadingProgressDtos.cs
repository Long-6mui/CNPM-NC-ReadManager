namespace ReadManager.Mobile.Models;
public class ReadingProgressDto
{
    public int StoryId { get; set; }
    public int ChapterId { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public decimal ScrollPercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public class ReadingStoryDto : ReadingProgressDto
{
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public int PublishedChapterCount { get; set; }
    public DateTime LastReadAt { get; set; }
}
public class SaveReadingProgressRequest
{
    public int ChapterId { get; set; }
    public decimal ScrollPercent { get; set; }
}

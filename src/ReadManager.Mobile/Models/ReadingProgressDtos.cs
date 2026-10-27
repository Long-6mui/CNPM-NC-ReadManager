namespace ReadManager.Mobile.Models;

// Khớp DTO thật của Backend 2 (DTOs/Progress/ReadingProgressDtos.cs, ReadingProgressController).

// PUT api/stories/{storyId}/progress   body: { chapterId, scrollPercent (0–100) }
public class SaveReadingProgressRequest
{
    public int ChapterId { get; set; }
    public decimal ScrollPercent { get; set; }
}

// GET api/me/reading  ->  PagedResultDto<ReadingStoryDto>
public class ReadingStoryDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public int ChapterId { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public decimal ScrollPercent { get; set; }
    public int PublishedChapterCount { get; set; }
    public bool HasNewChapters { get; set; }
    public DateTime LastReadAt { get; set; }     // UTC
}

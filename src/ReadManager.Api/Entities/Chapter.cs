namespace ReadManager.Api.Entities;

public class Chapter
{
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = "Free";
    public string PublicationStatus { get; set; } = "Draft";
    public DateTime? ScheduledAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Story Story { get; set; } = null!;
}

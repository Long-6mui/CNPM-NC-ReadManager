namespace ReadManager.Web.ViewModels;
public enum ChapterStatus { Draft, Reviewed }
public class Story
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = ""; public string Description { get; set; } = "";
    public string? CoverUrl { get; set; }
    public StoryStatus Status { get; set; }
    public AccessPolicy Access { get; set; }
    public decimal Price { get; set; }
    public int FreeChapterCount { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Chapter> Chapters { get; set; } = [];
}
public class Chapter
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public int No { get; set; }
    public string Title { get; set; } = ""; public string Content { get; set; } = "";
    public bool IsFree { get; set; }
    public ChapterStatus Status { get; set; }
    public DateTime? PublishAt { get; set; }
    public DateTime CreatedAt { get; set; }
    // PublishAt = giờ hẹn ra mắt (UTC); now truyền vào là DateTime.UtcNow
    public string StatusLabel(DateTime now) => Status == ChapterStatus.Draft ? "Nháp"
     : PublishAt.HasValue && now < PublishAt.Value ? "Hẹn giờ " + PublishAt.Value.ToLocalTime().ToString("HH:mm dd/MM/yyyy")
     : "Đã công khai";
}
namespace ReadManager.Web.ViewModels;

public enum AccessPolicy { Free, Mixed, Paid }
public enum StoryStatus { Ongoing, Completed, Paused }
public record GenreVm(int Id, string Name);
public class StoryCardVm
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string? CoverUrl { get; init; }
    public AccessPolicy Access { get; init; }
    public StoryStatus Status { get; init; }
    public int PublishedChapterCount { get; init; }
    public int GenreId { get; init; }
    public DateTime CreatedAtSort { get; init; }
    public DateTime UpdatedAtSort { get; init; }
}
public class HomeVm
{
    public List<StoryCardVm> Updated { get; init; } = [];
    public List<StoryCardVm> Newest { get; init; } = [];
    public List<StoryCardVm> Free { get; init; } = [];
    public List<GenreVm> Genres { get; init; } = [];
    public int TotalStories { get; init; }
    public int TotalGenres { get; init; }
    public int TotalChapters { get; init; }
    public string? Query { get; init; }
    public int? GenreId { get; init; }
}
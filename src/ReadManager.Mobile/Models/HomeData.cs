namespace ReadManager.Mobile.Models;

public class HomeData
{
    public int TotalStories { get; set; }
    public int TotalGenres { get; set; }
    public int TotalChapters { get; set; }
    public List<Genre> Genres { get; set; } = new();
    public List<Story> Updated { get; set; } = new();
    public List<Story> Newest { get; set; } = new();
    public List<Story> Free { get; set; } = new();
}
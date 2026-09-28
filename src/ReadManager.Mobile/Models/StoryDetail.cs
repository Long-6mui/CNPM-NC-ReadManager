namespace ReadManager.Mobile.Models;

// Dùng cho màn Chi tiết truyện
public class StoryDetail
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string PublicationStatus { get; set; } = "Ongoing";
    public string AccessPolicy { get; set; } = "Free";
    public decimal? CurrentPrice { get; set; }
    public DateTime? FirstPublishedAt { get; set; }
    public List<Genre> Genres { get; set; } = new();
}

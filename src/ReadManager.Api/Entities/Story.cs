namespace ReadManager.Api.Entities;

public class Story
{
    public int StoryId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string Synopsis { get; set; } = string.Empty;

    public string? CoverUrl { get; set; }

    public string PublicationStatus { get; set; } = "Ongoing";

    public string Visibility { get; set; } = "Draft";

    public string AccessPolicy { get; set; } = "Free";

    public decimal? CurrentPrice { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FirstPublishedAt { get; set; }

    // Người tạo truyện, liên kết qua CreatedBy.
    public User Creator { get; set; } = null!;
}
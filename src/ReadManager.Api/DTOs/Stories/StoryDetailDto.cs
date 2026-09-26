namespace ReadManager.Api.DTOs.Stories;

// Dữ liệu đầy đủ cho 1 truyện — dùng cho trang chi tiết (PB11) và cho form sửa ở Admin.
public class StoryDetailDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }

    public string PublicationStatus { get; set; } = string.Empty; // Ongoing | Completed | Paused
    public string Visibility { get; set; } = string.Empty;        // Draft | Public | Hidden
    public string AccessPolicy { get; set; } = string.Empty;      // Free | Mixed | Paid
    public decimal? CurrentPrice { get; set; }

    public int CreatedBy { get; set; }

    // Hai số này tính từ bảng Chapters tại thời điểm gọi API, không lưu cứng trên Story.
    public int PublishedChapterCount { get; set; }
    public int FreeChapterCount { get; set; }

    public List<GenreRefDto> Genres { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? FirstPublishedAt { get; set; }
}
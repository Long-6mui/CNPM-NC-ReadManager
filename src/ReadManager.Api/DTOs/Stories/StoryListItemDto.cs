namespace ReadManager.Api.DTOs.Stories;

// Dữ liệu cho MỖI Ô TRUYỆN trong danh sách/trang chủ.
// Cố tình không có Synopsis đầy đủ để response nhẹ khi trả về nhiều truyện cùng lúc;
// muốn xem đầy đủ thì gọi GET /api/stories/{id} (StoryDetailDto).
public class StoryListItemDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;   
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }

    public string PublicationStatus { get; set; } = string.Empty; // Ongoing | Completed | Paused
    public string AccessPolicy { get; set; } = string.Empty;      // Free | Mixed | Paid
    public decimal? CurrentPrice { get; set; }

    // Đếm từ bảng Chapters (PublicationStatus = Published), KHÔNG phải field lưu sẵn trên Story.
    public int PublishedChapterCount { get; set; }

    public List<string> Genres { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
namespace ReadManager.Mobile.Models;

// !! Backend 2 (ReadingProgress) CHƯA đưa lên main nên các tên trường/đường dẫn dưới đây là DỰ ĐOÁN.
// Khi Backend 2 xong: đối chiếu DTO của họ, sửa file này + ApiReadingProgressService.cs rồi bật ApiSettings.UseReadingProgressApi.
public class ReadingProgressDto
{
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int ChapterId { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class SaveReadingProgressRequest
{
    public int StoryId { get; set; }
    public int ChapterId { get; set; }
}

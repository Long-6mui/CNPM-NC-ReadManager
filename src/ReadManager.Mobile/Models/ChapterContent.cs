namespace ReadManager.Mobile.Models;

// Dùng cho màn Đọc chương (miễn phí — Sprint 1)
public class ChapterContent
{
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = "Free";

    // Điều hướng nhanh chương trước/sau (API có thể trả kèm hoặc mobile tự suy ra từ ChapterList)
    public int? PreviousChapterId { get; set; }
    public int? NextChapterId { get; set; }
}

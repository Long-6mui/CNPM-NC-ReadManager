namespace ReadManager.Api.DTOs.Statistics.Content;

// PB40 — SỐ LIỆU CỦA 1 CHƯƠNG (xem chương nào được đọc / bàn luận nhiều)
public class ChapterContentStatsDto
{
    public int ChapterId { get; set; }
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty;   // Free / Paid

    public int Reads { get; set; }
    public int UniqueReaders { get; set; }
    public int Comments { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Progress;

// PB28 — DỮ LIỆU GỬI LÊN KHI LƯU TIẾN ĐỘ ĐỌC
//   PUT api/stories/5/progress   body: { "chapterId": 12, "scrollPercent": 63.5 }
public class SaveReadingProgressDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Chương không hợp lệ.")]
    public int ChapterId { get; set; }

    // Vị trí cuộn trong chương: 0 = đầu chương, 100 = cuối chương
    [Range(0, 100, ErrorMessage = "Vị trí đọc phải từ 0 đến 100 (%).")]
    public decimal ScrollPercent { get; set; }
}

// PB28 — ĐIỂM ĐỌC TIẾP của 1 truyện (nút "Đọc tiếp")
public class ReadingProgressDto
{
    public int StoryId { get; set; }
    public int ChapterId { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public decimal ScrollPercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// PB29 — 1 truyện trong danh sách "Truyện đang đọc"
public class ReadingStoryDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }

    // Điểm đọc tiếp
    public int ChapterId { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public decimal ScrollPercent { get; set; }

    public int PublishedChapterCount { get; set; }   // tổng số chương đã ra mắt → vẽ thanh tiến độ
    public bool HasNewChapters { get; set; }         // còn chương phía sau chương đang đọc
    public DateTime LastReadAt { get; set; }         // lần đọc gần nhất (UTC)
}

namespace ReadManager.Api.DTOs.Chapters;

// PB12 — ĐỌC CHƯƠNG
// Dữ liệu cho màn hình đọc: có nội dung + nút chương trước/sau.

// LƯU Ý BẢO MẬT: chương trả phí mà độc giả chưa có quyền
// → vẫn trả về nhưng Content để TRỐNG và IsLocked = true.
//  nội dung trả phí không bao giờ bị lộ ra ngoài.
public class ChapterReadDto
{
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = string.Empty;  // Tên truyện (hiện trên đầu trang đọc)
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;     // Nội dung chương (rỗng nếu bị khóa)

    public string AccessLevel { get; set; } = "Free";
    public string PublicationStatus { get; set; } = "Draft";
    public bool IsLocked { get; set; }                       // true = cần mở khóa mới đọc được

    // ----- Nút "Chương trước" / "Chương sau" -----
    // null = không có (đang ở chương đầu hoặc chương cuối)
    // Mobile chuyển chương bằng Id, Web chuyển chương bằng số chương → trả cả 2.
    public int? PreviousChapterId { get; set; }
    public int? NextChapterId { get; set; }
    public int? PreviousChapterNumber { get; set; }
    public int? NextChapterNumber { get; set; }

    // Hẹn giờ ra mắt: IsUpcoming = true → chưa tới giờ, độc giả chưa đọc được (giờ ra mắt ở ScheduledAt, UTC)
    public DateTime? ScheduledAt { get; set; }
    public bool IsUpcoming { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
namespace ReadManager.Api.DTOs.Chapters;

// PB11 — DANH SÁCH CHƯƠNG
// Mỗi object = 1 dòng trong danh sách chương (vd: "Chương 3: Gặp gỡ 🔒").
// KHÔNG chứa nội dung chương để dữ liệu trả về nhẹ, load nhanh.

public class ChapterListItemDto
{
    public int ChapterId { get; set; }       
    public int StoryId { get; set; }       
    public int ChapterNumber { get; set; }  
    public string Title { get; set; } = string.Empty;   

    public string AccessLevel { get; set; } = "Free";      
    public string PublicationStatus { get; set; } = "Draft"; 

    // PB10 — true giao diện hiện ổ khóa 🔒
    public bool IsLocked { get; set; }

    public DateTime? PublishedAt { get; set; }  // Ngày công khai lần đầu (null nếu còn nháp)
    public DateTime CreatedAt { get; set; }     
    public DateTime UpdatedAt { get; set; }     
}
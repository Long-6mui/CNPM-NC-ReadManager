namespace ReadManager.Api.DTOs.Community;

// PB33 — 1 BÁO CÁO trong danh sách của Admin
public class ReportDto
{
    public int ReportId { get; set; }
    public int ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;   // tên người báo cáo

    public string TargetType { get; set; } = string.Empty;     // Story | Chapter | Comment
    public int TargetId { get; set; }
    public int? StoryId { get; set; }                           // để Admin bấm mở truyện
    public int? ChapterNumber { get; set; }                     // để Admin bấm mở chương
    public string TargetPreview { get; set; } = string.Empty;   // vd: "Chương 3: Gặp gỡ" / nội dung bình luận
    public bool TargetExists { get; set; }                      // false = đối tượng đã bị xóa/ẩn

    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending";             // Pending | Resolved | Rejected
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
}

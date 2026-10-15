using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Community;

// PB33 — ADMIN XỬ LÝ BÁO CÁO
public class HandleReportDto
{
    // "Resolved" = vi phạm thật, đã xử lý | "Rejected" = không vi phạm, bác bỏ
    [Required]
    [RegularExpression("^(Resolved|Rejected)$", ErrorMessage = "Status phải là Resolved hoặc Rejected.")]
    public string Status { get; set; } = string.Empty;

    // Báo cáo bình luận + Resolved: true = ẩn luôn bình luận đó
    public bool HideComment { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Community;

// PB33 — ĐỘC GIẢ GỬI BÁO CÁO VI PHẠM
public class CreateReportDto
{
    // Báo cáo cái gì: "Story" (truyện) | "Chapter" (chương) | "Comment" (bình luận)
    [Required]
    [RegularExpression("^(Story|Chapter|Comment)$", ErrorMessage = "TargetType phải là Story, Chapter hoặc Comment.")]
    public string TargetType { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "TargetId không hợp lệ.")]
    public int TargetId { get; set; }

    // Lý do — phải là 1 trong các lý do ở GET api/reports/reasons
    [Required(ErrorMessage = "Vui lòng chọn lý do.")]
    [StringLength(100)]
    public string Reason { get; set; } = string.Empty;

    // Ghi chú thêm (không bắt buộc)
    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }
}

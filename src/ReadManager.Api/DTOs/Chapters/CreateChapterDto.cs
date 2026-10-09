using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Chapters;

// PB07 + PB10 — TẠO CHƯƠNG MỚI

public class CreateChapterDto
{
    [Range(1, 100000, ErrorMessage = "Số chương phải từ 1 trở lên.")]
    public int ChapterNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề chương.")]
    [StringLength(255, ErrorMessage = "Tiêu đề chương tối đa 255 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung chương.")]
    [StringLength(200000, ErrorMessage = "Nội dung chương tối đa 200.000 ký tự.")]
    public string Content { get; set; } = string.Empty;

    // PB10 — thiết lập chương này miễn phí hay trả phí. Chỉ nhận "Free" hoặc "Paid".
    [RegularExpression("^(Free|Paid)$", ErrorMessage = "AccessLevel phải là Free hoặc Paid.")]
    public string AccessLevel { get; set; } = "Free";

    [RegularExpression("^(Draft|Published)$", ErrorMessage = "PublicationStatus phải là Draft hoặc Published.")]
    public string PublicationStatus { get; set; } = "Draft";

    // Hẹn giờ ra mắt (giờ UTC). Chỉ có tác dụng khi PublicationStatus = Published và thời điểm ở tương lai.
    // Trước giờ này: độc giả THẤY chương trong danh sách nhưng CHƯA ĐỌC được. Để trống = ra mắt ngay.
    public DateTime? ScheduledAt { get; set; }
}
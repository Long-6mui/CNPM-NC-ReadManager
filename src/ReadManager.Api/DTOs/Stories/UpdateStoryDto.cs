using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Stories;

// Dữ liệu client gửi lên khi cập nhật truyện đã có (PB05: sửa thông tin + đổi trạng thái
// phát hành; PB06: gán lại danh sách thể loại). Là PUT nên gửi đủ toàn bộ field,
// không phải PATCH từng phần.
public class UpdateStoryDto
{
    [Required(ErrorMessage = "Vui lòng nhập tên truyện.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    public string AuthorName { get; set; } = string.Empty;

    public string Synopsis { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? CoverUrl { get; set; }

    [Required]
    [RegularExpression("^(Ongoing|Completed|Paused)$", ErrorMessage = "PublicationStatus phải là Ongoing, Completed hoặc Paused.")]
    public string PublicationStatus { get; set; } = "Ongoing";

    [Required]
    [RegularExpression("^(Draft|Public|Hidden)$", ErrorMessage = "Visibility phải là Draft, Public hoặc Hidden.")]
    public string Visibility { get; set; } = "Draft";

    [Required]
    [RegularExpression("^(Free|Mixed|Paid)$", ErrorMessage = "AccessPolicy phải là Free, Mixed hoặc Paid.")]
    public string AccessPolicy { get; set; } = "Free";

    [Range(0.01, 100000000, ErrorMessage = "Giá phải lớn hơn 0.")]
    public decimal? CurrentPrice { get; set; }

    // Danh sách GenreId đầy đủ mà truyện này nên có SAU khi cập nhật.
    // StoryService sẽ tự so sánh với danh sách hiện tại để thêm/xóa cho đúng — PB06.
    public List<int> GenreIds { get; set; } = new();
}
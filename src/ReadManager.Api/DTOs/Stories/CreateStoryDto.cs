using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Stories;

// Dữ liệu client gửi lên khi tạo truyện mới (PB04).
// PublicationStatus KHÔNG có ở đây: truyện mới luôn bắt đầu ở "Ongoing",
// đổi trạng thái phát hành là việc của PB05 (Update).
public class CreateStoryDto
{
    [Required(ErrorMessage = "Vui lòng nhập tên truyện.")]
    [StringLength(255, ErrorMessage = "Tên truyện tối đa 255 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    public string AuthorName { get; set; } = string.Empty;

    public string Synopsis { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? CoverUrl { get; set; }

    // Chỉ nhận đúng 3 giá trị này, khớp enum AccessPolicy bên Web và cột AccessPolicy trong DB.
    [RegularExpression("^(Free|Mixed|Paid)$", ErrorMessage = "AccessPolicy phải là Free, Mixed hoặc Paid.")]
    public string AccessPolicy { get; set; } = "Free";

    // Mặc định Draft: truyện mới tạo chưa hiện công khai ngay.
    [RegularExpression("^(Draft|Public|Hidden)$", ErrorMessage = "Visibility phải là Draft, Public hoặc Hidden.")]
    public string Visibility { get; set; } = "Draft";

    // Chỉ áp dụng khi AccessPolicy khác Free — StoryService sẽ kiểm tra lại lần nữa.
    [Range(0.01, 100000000, ErrorMessage = "Giá phải lớn hơn 0.")]
    public decimal? CurrentPrice { get; set; }

    public List<int> GenreIds { get; set; } = new();

    // TODO(auth): khi PB02/PB03 (đăng nhập, phân quyền) xong, lấy UserId từ
    // người dùng đang đăng nhập (User.Identity) thay vì nhận trực tiếp từ client
    // như hiện tại — vì hiện Program.cs của Api chưa bật Authentication.
    [Required]
    public int CreatedBy { get; set; }
}
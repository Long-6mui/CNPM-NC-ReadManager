using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Genres;

// Dữ liệu client gửi lên khi tạo thể loại mới.
// Slug được server tự sinh từ Name, client không cần gửi.
public class CreateGenreDto
{
    [Required(ErrorMessage = "Vui lòng nhập tên thể loại.")]
    [StringLength(100, ErrorMessage = "Tên thể loại tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;
}
using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Chapters;

// PB08 — TẢI LÊN NHIỀU CHƯƠNG CÙNG LÚC (dạng JSON)
public class UploadChaptersDto
{
    [Required]
    [MinLength(1, ErrorMessage = "Cần ít nhất 1 chương.")]
    [MaxLength(500, ErrorMessage = "Mỗi lần tải tối đa 500 chương.")]
    public List<CreateChapterDto> Chapters { get; set; } = new();

    // true = ghi đè chương trùng số, false = bỏ qua
    public bool OverwriteExisting { get; set; }
}
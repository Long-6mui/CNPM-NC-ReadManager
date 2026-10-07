using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Chapters;

// PB08 — TẢI LÊN NHIỀU CHƯƠNG CÙNG LÚC
// Admin dán cả đoạn văn dài → Web tự tách thành từng chương
// → gửi lên API 1 lần bằng object này (thay vì bấm "Thêm chương" 50 lần).
public class UploadChaptersDto
{
    // Danh sách các chương cần lưu (mỗi chương có cấu trúc giống lúc tạo 1 chương).
    [Required]
    [MinLength(1, ErrorMessage = "Cần ít nhất 1 chương.")]
    [MaxLength(500, ErrorMessage = "Mỗi lần tải tối đa 500 chương.")]
    public List<CreateChapterDto> Chapters { get; set; } = new();

    // Nếu truyện ĐÃ CÓ chương trùng số 
    // true  = ghi đè nội dung mới lên chương cũ
    // false = bỏ qua chương đó, báo lại số chương bị bỏ qua
    public bool OverwriteExisting { get; set; }
}
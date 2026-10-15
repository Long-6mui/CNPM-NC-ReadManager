using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Community;

// PB32 — DỮ LIỆU GỬI LÊN KHI ĐĂNG BÌNH LUẬN
public class CreateCommentDto
{
    // Bình luận cho chương nào. Để trống (null) = bình luận chung cho cả truyện.
    public int? ChapterId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung bình luận.")]
    [StringLength(1000, ErrorMessage = "Bình luận tối đa 1000 ký tự.")]
    public string Content { get; set; } = string.Empty;
}
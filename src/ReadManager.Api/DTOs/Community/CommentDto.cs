namespace ReadManager.Api.DTOs.Community;

// PB32 — 1 BÌNH LUẬN trả về cho Web/Mobile hiển thị.
public class CommentDto
{
    public int CommentId { get; set; }
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }
    public int? ChapterNumber { get; set; }     // để hiện "Chương 5" cạnh bình luận (null = bình luận chung)

    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;   // tên người viết
    public string? AvatarUrl { get; set; }
    public string? Title { get; set; }          // Sprint 5 — danh hiệu chuỗi đọc (vd: "Mọt Sách Tập Sự")
    public string? AvatarFrame { get; set; }    // Sprint 5 — khung avatar: frame_dong | frame_bac | frame_kim_cuong

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }     // giờ UTC

    // true = người đang xem là người viết hoặc Admin → hiện nút "Xóa"
    public bool CanDelete { get; set; }
}

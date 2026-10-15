namespace ReadManager.Api.Entities;

// PB32 — BÌNH LUẬN của độc giả trên truyện (hoặc trên 1 chương cụ thể).
// Status: "Visible" = hiện | "Hidden" = bị ẩn (do admin xử lý báo cáo / người viết xóa)
public class Comment
{
    public int CommentId { get; set; }
    public int UserId { get; set; }          // người viết
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }      // null = bình luận chung cho cả truyện
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "Visible";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
    public Chapter? Chapter { get; set; }
}

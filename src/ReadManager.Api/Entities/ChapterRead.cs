namespace ReadManager.Api.Entities;

// PB40 (BE3 — thống kê nội dung) — 1 LƯỢT ĐỌC CHƯƠNG
// Ghi lại mỗi khi độc giả mở đọc được nội dung 1 chương (không tính Admin xem trước,
// không tính chương bị khóa / chưa tới giờ ra mắt).
// Người đã đăng nhập: mở lại cùng chương trong 30 phút chỉ tính 1 lượt (tránh F5 làm tăng ảo).
public class ChapterRead
{
    public int ChapterReadId { get; set; }
    public int StoryId { get; set; }        // lưu sẵn để thống kê theo truyện / thể loại cho nhanh
    public int ChapterId { get; set; }
    public int? UserId { get; set; }        // null = khách chưa đăng nhập
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;   // giờ UTC

    public Story Story { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
    public User? User { get; set; }
}

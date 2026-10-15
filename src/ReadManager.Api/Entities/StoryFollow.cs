namespace ReadManager.Api.Entities;

// PB31 — Người dùng THEO DÕI truyện → có chương mới sẽ nhận thông báo.
// Khóa chính = (UserId, StoryId) → 1 người chỉ theo dõi 1 truyện 1 lần.
public class StoryFollow
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
}

namespace ReadManager.Api.DTOs.Community;

// PB31 — TRẠNG THÁI THEO DÕI 1 truyện (hiện nút "Theo dõi" / "Đang theo dõi")
public class FollowStatusDto
{
    public int StoryId { get; set; }
    public bool IsFollowing { get; set; }     // người đang xem có theo dõi không (khách = false)
    public int FollowerCount { get; set; }    // tổng số người theo dõi
}

// PB31 — 1 truyện trong danh sách "Truyện đang theo dõi"
public class FollowedStoryDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public DateTime FollowedAt { get; set; }
}

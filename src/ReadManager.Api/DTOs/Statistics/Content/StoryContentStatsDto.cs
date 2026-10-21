namespace ReadManager.Api.DTOs.Statistics.Content;

// PB40 — SỐ LIỆU QUAN TÂM CỦA 1 TRUYỆN trong khoảng thời gian đang xem
public class StoryContentStatsDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;   // Public / Draft / Hidden

    public int Reads { get; set; }            // lượt đọc chương
    public int UniqueReaders { get; set; }    // số độc giả (đã đăng nhập) khác nhau đã đọc
    public int NewFollows { get; set; }       // lượt theo dõi mới trong khoảng thời gian
    public int TotalFollowers { get; set; }   // tổng người đang theo dõi (tính tới hiện tại)
    public int Purchases { get; set; }        // lượt mua trọn bộ đã thanh toán (đơn Paid)
    public int Comments { get; set; }         // số bình luận (đang hiện)

    // Điểm quan tâm tổng hợp để xếp hạng:
    //   đọc × 1 + theo dõi mới × 5 + bình luận × 3 + lượt mua × 10
    public int InterestScore { get; set; }
}

namespace ReadManager.Api.DTOs.Statistics.Content;

// PB40 — SỐ LIỆU THEO THỂ LOẠI (cộng dồn các truyện thuộc thể loại đó)
// 1 truyện có nhiều thể loại → được tính vào TỪNG thể loại của nó.
public class GenreContentStatsDto
{
    public int GenreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int StoryCount { get; set; }       // số truyện thuộc thể loại

    public int Reads { get; set; }
    public int NewFollows { get; set; }
    public int Purchases { get; set; }
    public int Comments { get; set; }
    public int InterestScore { get; set; }
}

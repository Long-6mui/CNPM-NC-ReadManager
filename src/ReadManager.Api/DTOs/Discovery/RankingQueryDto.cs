namespace ReadManager.Api.DTOs.Discovery;

// Query parameters cho bảng xếp hạng truyện
public class RankingQueryDto
{
    // reads | follows | score
    public string By { get; set; } = "reads";

    // day | week | month | all
    public string Period { get; set; } = "week";

    public int? GenreId { get; set; }

    public int Top { get; set; } = 10;
}

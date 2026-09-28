namespace ReadManager.Api.DTOs.Genres;

// Dữ liệu trả về cho client (Web/Mobile) khi liệt kê thể loại.
public class GenreDto
{
    public int GenreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    // Số truyện đang gán thể loại này — Admin cần số này để quyết định
    // có cho xóa thể loại hay không (không cho xóa nếu > 0).
    public int StoryCount { get; set; }
}
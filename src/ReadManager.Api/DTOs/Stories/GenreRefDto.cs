namespace ReadManager.Api.DTOs.Stories;

// Thông tin thể loại rút gọn, chỉ để hiển thị kèm theo 1 truyện.
// Tách riêng khỏi Genres.GenreDto vì ở đây không cần StoryCount.
public record GenreRefDto(int GenreId, string Name);
using Microsoft.EntityFrameworkCore;

namespace ReadManager.Api.Entities;

[PrimaryKey(nameof(UserId), nameof(StoryId))]
public class ReaderFavorite
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int StoryId { get; set; }
    public Story Story { get; set; } = null!;
}

[PrimaryKey(nameof(UserId), nameof(GenreId))]
public class ReaderGenre
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}

[PrimaryKey(nameof(UserId), nameof(ChapterId))]
public class ChapterReward
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
}

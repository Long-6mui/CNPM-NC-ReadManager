namespace ReadManager.Api.Entities;

public class StoryGenre
{
    public int StoryId { get; set; }
    public int GenreId { get; set; }
    public Story Story { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
}

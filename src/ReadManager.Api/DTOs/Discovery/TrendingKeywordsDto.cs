using ReadManager.Api.DTOs.Stories;

namespace ReadManager.Api.DTOs.Discovery;

public class TrendingKeywordsDto
{
    public List<string> Keywords { get; set; } = new();
    public List<string> HotGenres { get; set; } = new();
    public List<StoryListItemDto> FeaturedStories { get; set; } = new();
}

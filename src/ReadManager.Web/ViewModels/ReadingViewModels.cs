namespace ReadManager.Web.ViewModels;
public class BrowseVm
{
    public List<StoryCardVm> Stories { get; set; } = new();
    public List<GenreVm> Genres { get; set; } = new();
    public string? Q { get; set; }
    public int? GenreId { get; set; }
    public AccessPolicy? Access { get; set; }
    public StoryStatus? Status { get; set; }
    public string Sort { get; set; } = "updated";
}

public class ChapterRowVm
{
    public int No { get; set; }
    public string Title { get; set; } = "";
    public bool Locked { get; set; }
    public bool NotYetPublicPreview { get; set; } // chỉ true khi admin xem trước chương chưa công khai
    public string? StatusLabel { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StoryDetailVm
{
    public Story Story { get; set; } = null!;
    public List<string> GenreNames { get; set; } = new();
    public List<ChapterRowVm> Chapters { get; set; } = new();
    public bool IsFollowing { get; set; }
    public bool Owned { get; set; }
    public bool HasPendingOrder { get; set; }
    public int? ContinueChapterNo { get; set; }
    public int? FirstVisibleChapterNo { get; set; }
}

public class ReadVm
{
    public Story Story { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
    public bool Locked { get; set; }
    public bool NotYetPublicPreview { get; set; }
    public int? PrevNo { get; set; }
    public int? NextNo { get; set; }
}


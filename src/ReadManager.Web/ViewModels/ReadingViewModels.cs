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
    public DateTime? UpcomingAt { get; set; }   // có giá trị = chương sắp ra mắt (giờ Việt Nam), chưa đọc được
}

public class StoryDetailVm
{
    public Story Story { get; set; } = null!;
    public List<string> GenreNames { get; set; } = new();
    public List<ChapterRowVm> Chapters { get; set; } = new();   // chỉ các chương của TRANG đang xem

    // Phân trang danh sách chương (mỗi trang 20 chương, chia 2 cột)
    public int TotalChapters { get; set; }
    public int ChapterPage { get; set; } = 1;
    public int ChapterPageCount { get; set; } = 1;

    public bool IsFollowing { get; set; }
    public int FollowerCount { get; set; }      // PB31 — số người theo dõi
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
    public DateTime? UpcomingAt { get; set; }   // có giá trị = chưa tới giờ ra mắt (giờ Việt Nam)
    public bool HasEntitlement { get; set; }    // PB22 — người đọc đã có quyền (gói/mua riêng)
    public int? PrevNo { get; set; }
    public int? NextNo { get; set; }

    // PB32 — bình luận của chương này
    public List<ReadManager.Web.Services.CommentApi> Comments { get; set; } = new();
    public int CommentTotal { get; set; }

    // PB33 — các lý do báo cáo vi phạm (lấy từ API)
    public List<string> ReportReasons { get; set; } = new();
}

// PB31 — trang "Thông báo của tôi"
public class NotificationsVm
{
    public List<ReadManager.Web.Services.NotificationApi> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageCount { get; set; } = 1;
}

// PB33 — trang Admin "Báo cáo vi phạm"
public class AdminReportsVm
{
    public List<ReadManager.Web.Services.ReportApi> Items { get; set; } = new();
    public string? Status { get; set; }          // bộ lọc đang chọn (null = tất cả)
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageCount { get; set; } = 1;
}
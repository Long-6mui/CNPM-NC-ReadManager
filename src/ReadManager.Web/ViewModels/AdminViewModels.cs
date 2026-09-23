using System.ComponentModel.DataAnnotations;


namespace ReadManager.Web.ViewModels;

public class StoryFormVm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên truyện.")]
    [Display(Name = "Tên truyện")]
    public string Title { get; set; } = "";

    [Display(Name = "Tác giả")]
    public string Author { get; set; } = "";

    [Display(Name = "Giới thiệu")]
    public string Description { get; set; } = "";

    public StoryStatus Status { get; set; } = StoryStatus.Ongoing;
    public AccessPolicy Access { get; set; } = AccessPolicy.Free;

    [Display(Name = "Số chương đọc miễn phí")]
    public int FreeChapterCount { get; set; }
    public string Visibility { get; set; } = "Draft";

    [Display(Name = "Giá mua trọn bộ (VNĐ)")]
    public decimal Price { get; set; }

    public string? CoverUrl { get; set; }

    [Display(Name = "Thể loại")]
    public List<int> SelectedGenreIds { get; set; } = new();

    public List<GenreVm> AllGenres { get; set; } = new();
    public List<Chapter> Chapters { get; set; } = new();
}

public class ChapterFormVm
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = "";

    [Range(1, int.MaxValue)] public int No { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề chương.")]
    public string Title { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập nội dung chương.")]
    public string Content { get; set; } = "";
    public bool IsFree { get; set; }
    public ChapterStatus Status { get; set; } = ChapterStatus.Reviewed;

    [Display(Name = "Hẹn giờ đăng (để trống = đăng ngay)")]
    [DataType(DataType.DateTime)]
    public DateTime? PublishAt { get; set; }
}

public class BulkImportVm
{
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = "";
    [Display(Name = "Dán nội dung nhiều chương")]
    public string RawText { get; set; } = "";
    public bool MarkFree { get; set; }
}

public class GenreFormVm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên thể loại.")]
    public string Name { get; set; } = "";
}


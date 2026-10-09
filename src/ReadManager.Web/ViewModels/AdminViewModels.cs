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

// Form thêm / sửa 1 chương (BE4)
public class ChapterFormVm
{
    public int Id { get; set; }               // 0 = chương mới
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = "";

    [Range(1, 100000, ErrorMessage = "Số chương phải từ 1 trở lên.")]
    [Display(Name = "Số chương")]
    public int No { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề chương.")]
    [StringLength(255, ErrorMessage = "Tiêu đề tối đa 255 ký tự.")]
    [Display(Name = "Tiêu đề chương")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập nội dung chương.")]
    [StringLength(200000, ErrorMessage = "Nội dung tối đa 200.000 ký tự.")]
    [Display(Name = "Nội dung")]
    public string Content { get; set; } = "";

    public bool IsFree { get; set; }                                         // PB10 — miễn phí?
    public ChapterStatus Status { get; set; } = ChapterStatus.Reviewed;      // Reviewed = công khai

    [Display(Name = "Hẹn giờ ra mắt (để trống = ra mắt ngay)")]
    [DataType(DataType.DateTime)]
    public DateTime? PublishAt { get; set; }
}

// Trang tải nhiều chương (BE4)
public class BulkImportVm
{
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = "";
    [Display(Name = "Dán nội dung nhiều chương")]
    public string RawText { get; set; } = "";
    public bool MarkFree { get; set; }
    public bool Publish { get; set; } = true;          // true = công khai ngay, false = lưu nháp
    public bool OverwriteExisting { get; set; }        // true = ghi đè chương trùng số
}

public class GenreFormVm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên thể loại.")]
    public string Name { get; set; } = "";
}
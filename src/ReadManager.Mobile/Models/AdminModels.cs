using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// ===== Dữ liệu trả về từ API quản trị =====

// 1 dòng trong danh sách chương của truyện (GET api/stories/{id}/chapters/admin)
public class AdminChapterRow
{
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = "Free";           // Free | Paid
    public string PublicationStatus { get; set; } = "Draft";    // Draft | Published
    public bool IsLocked { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string Header => $"Chương {ChapterNumber}: {Title}";
    public string FreeText => AccessLevel == "Free" ? "Miễn phí" : "Trả phí";
    public bool IsPublished => PublicationStatus == "Published";
    public string StatusText => IsPublished ? "Công khai" : "Nháp";
    public string SubLine => $"{FreeText} · {StatusText}";
}

// Nội dung 1 chương để sửa (GET api/stories/{id}/chapters/{no})
public class AdminChapterDetail
{
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = string.Empty;
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = "Free";
    public string PublicationStatus { get; set; } = "Draft";
}

// Kết quả "Kiểm tra / Lưu" khi tải nhiều chương (POST api/stories/{id}/chapters/upload-files)
public class UploadFilesResult
{
    public bool CanSave { get; set; }
    public bool Saved { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<ChapterPreviewRow> Chapters { get; set; } = new();
    public int Created { get; set; }
    public int Updated { get; set; }
    public List<int> SkippedNumbers { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

// 1 dòng bảng xem trước
public class ChapterPreviewRow
{
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ContentLength { get; set; }
    public string ContentPreview { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string NumberFrom { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;   // Thêm mới | Ghi đè | Bỏ qua | Lỗi
    public bool HasError { get; set; }
    public List<string> Problems { get; set; } = new();

    public string Header => $"Chương {ChapterNumber}: {Title}";
    public string Detail => $"{Action} · {ContentLength:N0} ký tự · {Source}";
    public string ProblemText => string.Join(Environment.NewLine, Problems);
    public bool HasProblems => Problems.Count > 0;
    public Color ActionColor => HasError ? Color.FromArgb("#E5484D")
        : HasProblems ? Color.FromArgb("#B7791F") : Color.FromArgb("#5B6B88");
}

// Ô chọn thể loại trong form sửa truyện
public partial class SelectableGenre : ObservableObject
{
    public int GenreId { get; set; }
    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}

// ===== Dữ liệu gửi lên API (tên trường khớp DTO của API) =====

public class CreateStoryRequest
{
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string AccessPolicy { get; set; } = "Free";
    public string Visibility { get; set; } = "Draft";
    public decimal? CurrentPrice { get; set; }
    public List<int> GenreIds { get; set; } = new();
    public int CreatedBy { get; set; }
}

public class UpdateStoryRequest
{
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string PublicationStatus { get; set; } = "Ongoing";
    public string AccessPolicy { get; set; } = "Free";
    public string Visibility { get; set; } = "Draft";
    public decimal? CurrentPrice { get; set; }
    public List<int> GenreIds { get; set; } = new();
}

public class ChapterRequest
{
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = "Free";
    public string PublicationStatus { get; set; } = "Draft";
}

public class GenreRequest
{
    public string Name { get; set; } = string.Empty;
}

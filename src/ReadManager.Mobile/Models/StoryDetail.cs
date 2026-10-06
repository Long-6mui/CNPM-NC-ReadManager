using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// Dùng cho màn Chi tiết truyện. Tên trường khớp StoryDetailDto của API.
public class StoryDetail
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string PublicationStatus { get; set; } = "Ongoing";
    public string AccessPolicy { get; set; } = "Free";
    public decimal? CurrentPrice { get; set; }
    public DateTime? FirstPublishedAt { get; set; }
    public List<Genre> Genres { get; set; } = new();

    public int PublishedChapterCount { get; set; }
    public int FreeChapterCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ----- Chỉ để hiển thị (không đọc từ JSON) -----
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    public bool HasNoCover => !HasCover;
    public string AccessLabel => StoryLabels.Access(AccessPolicy);
    public Color AccessColor => StoryLabels.AccessColor(AccessPolicy);
    public string StatusLabel => StoryLabels.Status(PublicationStatus);
    public string AuthorText => string.IsNullOrWhiteSpace(AuthorName) ? "Khuyết danh" : AuthorName;
    public List<string> Tags => Genres.Select(g => g.Name).Append(StatusLabel).ToList();
    public string PriceText => $"{(CurrentPrice ?? 0):N0}₫";
    public string PriceCaption => AccessPolicy == "Paid" ? "Giá trọn bộ" : "Mua trọn bộ";
    public string FreeChaptersText => FreeChapterCount >= 999 ? "Tất cả" : FreeChapterCount.ToString();
    public string UpdatedText => UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy");
    public bool HasChapters => PublishedChapterCount > 0;
}

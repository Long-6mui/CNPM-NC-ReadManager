using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// Dùng cho thẻ truyện ở Trang chủ / Khám phá. Tên trường khớp StoryListItemDto của API.
public class StorySummary
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string PublicationStatus { get; set; } = "Ongoing"; // Ongoing | Completed | Paused
    public string AccessPolicy { get; set; } = "Free";         // Free | Mixed | Paid
    public decimal? CurrentPrice { get; set; }
    public int PublishedChapterCount { get; set; }

    // API trả danh sách tên thể loại ở trường "genres"
    [JsonPropertyName("genres")]
    public List<string> GenreNames { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ----- Chỉ để hiển thị (không đọc từ JSON) -----
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    public bool HasNoCover => !HasCover;
    public string AccessLabel => StoryLabels.Access(AccessPolicy);
    public Color AccessColor => StoryLabels.AccessColor(AccessPolicy);
    public string StatusLabel => StoryLabels.Status(PublicationStatus);
    public string MetaText => PublishedChapterCount > 0 ? $"{AuthorName} · {PublishedChapterCount} chương" : AuthorName;
    public string ChapterText => PublishedChapterCount > 0 ? $"Chương {PublishedChapterCount}" : "Chưa có chương";
    public string AgoText => StoryLabels.Ago(UpdatedAt);
}

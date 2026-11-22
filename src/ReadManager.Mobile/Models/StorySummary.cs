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

    // Chỉ có khi lấy từ bảng xếp hạng (GET api/ranking/stories — StoryRankingItemDto)
    public int Rank { get; set; }
    public int MetricCount { get; set; }
    public int TotalReads { get; set; }
    public int TotalFollows { get; set; }

    // ----- Chỉ để hiển thị (không đọc từ JSON) -----
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    public bool HasNoCover => !HasCover;
    public string AccessLabel => StoryLabels.Access(AccessPolicy);
    public Color AccessColor => StoryLabels.AccessColor(AccessPolicy);
    public string StatusLabel => StoryLabels.Status(PublicationStatus);
    public string MetaText => PublishedChapterCount > 0 ? $"{AuthorName} · {PublishedChapterCount} chương" : AuthorName;
    public string ChapterText => PublishedChapterCount > 0 ? $"Chương {PublishedChapterCount}" : "Chưa có chương";
    public string AgoText => StoryLabels.Ago(UpdatedAt);
    public string UpdatedText => UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy");

    // ----- Giao diện mới (trang chủ, đề xuất, tìm kiếm) -----
    public string AuthorText => string.IsNullOrWhiteSpace(AuthorName) ? "Khuyết danh" : AuthorName;
    public string ChapterBadge => $"{PublishedChapterCount} CHƯƠNG";
    public string GenreLine => GenreNames.Count == 0 ? StatusLabel : string.Join(" · ", GenreNames.Take(3));

    // Huy chương hạng 1-2-3 (vàng / bạc / đồng), từ hạng 4 hiện số
    public bool IsTop3 => Rank is >= 1 and <= 3;
    public bool IsRankNumber => Rank > 3;
    public string RankText => Rank.ToString();
    public Color MedalColor => Rank switch
    {
        1 => Color.FromArgb("#F59E0B"),
        2 => Color.FromArgb("#94A3B8"),
        _ => Color.FromArgb("#EA7C3C")
    };

    // Do ViewModel gán sau khi tải (vd "1.240 lượt đọc", mô tả ngắn của truyện hạng 1)
    [JsonIgnore] public string MetricText { get; set; } = string.Empty;
    [JsonIgnore] public string Blurb { get; set; } = string.Empty;
}

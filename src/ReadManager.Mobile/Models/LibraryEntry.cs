using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// Một truyện trong "Tủ sách" (lịch sử xem hoặc yêu thích). Lưu trên máy dưới dạng JSON.
public class LibraryEntry
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string AccessPolicy { get; set; } = "Free";
    public string PublicationStatus { get; set; } = "Ongoing";
    public int PublishedChapterCount { get; set; }

    public DateTime ViewedAt { get; set; } = DateTime.UtcNow; // lần xem gần nhất (UTC)
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;  // lúc bấm yêu thích (UTC)

    // Chương đọc gần nhất (nếu có)
    public int LastChapterId { get; set; }
    public int LastChapterNumber { get; set; }
    public string LastChapterTitle { get; set; } = string.Empty;

    // ----- Chỉ để hiển thị: [JsonIgnore] để không bị lưu vào JSON -----
    [JsonIgnore] public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    [JsonIgnore] public bool HasNoCover => !HasCover;
    [JsonIgnore] public string AccessLabel => StoryLabels.Access(AccessPolicy);
    [JsonIgnore] public Color AccessColor => StoryLabels.AccessColor(AccessPolicy);
    [JsonIgnore] public string StatusLabel => StoryLabels.Status(PublicationStatus);
    [JsonIgnore] public string AuthorText => string.IsNullOrWhiteSpace(AuthorName) ? "Khuyết danh" : AuthorName;
    [JsonIgnore] public string MetaText => PublishedChapterCount > 0 ? $"{AuthorText} · {PublishedChapterCount} chương" : AuthorText;
    [JsonIgnore] public string ViewedText => $"Xem {StoryLabels.Ago(ViewedAt)}";
    [JsonIgnore] public string SavedText => $"Đã lưu {StoryLabels.Ago(SavedAt)}";
    [JsonIgnore] public bool CanContinue => LastChapterId > 0;
    [JsonIgnore] public string ProgressText => LastChapterNumber > 0 ? $"Đã đọc đến chương {LastChapterNumber}" : "Chưa đọc chương nào";
}

using System.Text.Json;
using Microsoft.Maui.Storage;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.Services;

public class LibraryService : ILibraryService
{
    private const int MaxHistory = 30;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IAuthService _auth;

    public LibraryService(IAuthService auth)
    {
        _auth = auth;
    }

    // Mỗi tài khoản một tủ sách riêng; chưa đăng nhập dùng tủ "khách" (0).
    private string HistoryKey => $"library.history.{_auth.CurrentUser?.UserId ?? 0}";
    private string FavoriteKey => $"library.favorites.{_auth.CurrentUser?.UserId ?? 0}";

    public IReadOnlyList<LibraryEntry> GetHistory()
        => Load(HistoryKey).OrderByDescending(e => e.ViewedAt).ToList();

    public IReadOnlyList<LibraryEntry> GetFavorites()
        => Load(FavoriteKey).OrderByDescending(e => e.SavedAt).ToList();

    public void AddToHistory(StoryDetail story)
    {
        var list = Load(HistoryKey);
        var entry = list.FirstOrDefault(e => e.StoryId == story.StoryId);
        if (entry is null)
        {
            entry = new LibraryEntry { StoryId = story.StoryId };
            list.Add(entry);
        }
        CopyInfo(entry, story);
        entry.ViewedAt = DateTime.UtcNow;

        // chỉ giữ MaxHistory truyện xem gần nhất
        var trimmed = list.OrderByDescending(e => e.ViewedAt).Take(MaxHistory).ToList();
        Save(HistoryKey, trimmed);

        // cập nhật thông tin mới nhất cho truyện đã yêu thích (tên, bìa, số chương)
        var favorites = Load(FavoriteKey);
        var fav = favorites.FirstOrDefault(e => e.StoryId == story.StoryId);
        if (fav is not null)
        {
            CopyInfo(fav, story);
            Save(FavoriteKey, favorites);
        }
    }

    public void RecordChapter(int storyId, int chapterId, int chapterNumber, string title)
    {
        var list = Load(HistoryKey);
        var entry = list.FirstOrDefault(e => e.StoryId == storyId);
        if (entry is null) return; // chưa có trong lịch sử (vào thẳng chương) thì bỏ qua
        entry.LastChapterId = chapterId;
        entry.LastChapterNumber = chapterNumber;
        entry.LastChapterTitle = title;
        entry.ViewedAt = DateTime.UtcNow;
        Save(HistoryKey, list);
    }

    public void RemoveFromHistory(int storyId)
    {
        var list = Load(HistoryKey);
        if (list.RemoveAll(e => e.StoryId == storyId) > 0) Save(HistoryKey, list);
    }

    public void ClearHistory() => Preferences.Default.Remove(HistoryKey);

    public bool IsFavorite(int storyId) => Load(FavoriteKey).Any(e => e.StoryId == storyId);

    public bool ToggleFavorite(StoryDetail story)
    {
        var list = Load(FavoriteKey);
        var existing = list.FirstOrDefault(e => e.StoryId == story.StoryId);
        if (existing is not null)
        {
            list.Remove(existing);
            Save(FavoriteKey, list);
            return false;
        }

        var entry = new LibraryEntry { StoryId = story.StoryId, SavedAt = DateTime.UtcNow };
        CopyInfo(entry, story);
        list.Add(entry);
        Save(FavoriteKey, list);
        return true;
    }

    public void RemoveFavorite(int storyId)
    {
        var list = Load(FavoriteKey);
        if (list.RemoveAll(e => e.StoryId == storyId) > 0) Save(FavoriteKey, list);
    }

    // ---------------------------------------------------------------

    private static void CopyInfo(LibraryEntry entry, StoryDetail story)
    {
        entry.Title = story.Title;
        entry.AuthorName = story.AuthorName;
        entry.CoverUrl = story.CoverUrl;
        entry.AccessPolicy = story.AccessPolicy;
        entry.PublicationStatus = story.PublicationStatus;
        entry.PublishedChapterCount = story.PublishedChapterCount;
    }

    private static List<LibraryEntry> Load(string key)
    {
        try
        {
            var raw = Preferences.Default.Get(key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return new List<LibraryEntry>();
            return JsonSerializer.Deserialize<List<LibraryEntry>>(raw, Json) ?? new List<LibraryEntry>();
        }
        catch (JsonException)
        {
            return new List<LibraryEntry>(); // dữ liệu hỏng -> coi như trống
        }
    }

    private static void Save(string key, List<LibraryEntry> list)
        => Preferences.Default.Set(key, JsonSerializer.Serialize(list, Json));
}

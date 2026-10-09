using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// "Tủ sách": lịch sử xem + truyện yêu thích. API chưa có chức năng này nên lưu ngay trên máy (theo từng tài khoản).
public interface ILibraryService
{
    IReadOnlyList<LibraryEntry> GetHistory();
    IReadOnlyList<LibraryEntry> GetFavorites();

    void AddToHistory(StoryDetail story);                                            // mở chi tiết truyện -> ghi lịch sử
    void RecordChapter(int storyId, int chapterId, int chapterNumber, string title); // đọc chương -> nhớ chương đang đọc
    void RemoveFromHistory(int storyId);
    void ClearHistory();

    bool IsFavorite(int storyId);
    bool ToggleFavorite(StoryDetail story);                                          // trả về true nếu giờ đang là "yêu thích"
    void RemoveFavorite(int storyId);
}

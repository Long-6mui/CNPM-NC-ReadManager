namespace ReadManager.Web.Services;

public class ReadingProgress
{
    public string UserId { get; set; } = "";
    public int StoryId { get; set; }
    public string StoryTitle { get; set; } = "";
    public int ChapterNo { get; set; }
    public string ChapterTitle { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// PB28 + PB29 — DỮ LIỆU MẪU (trong bộ nhớ): chương đọc gần nhất của từng truyện, theo từng người dùng.
// Backend 2 (nhánh feature/s3-members-progress, ReadingProgressController) chưa có trên main.
// Khi API có, thay thân các hàm dưới đây bằng lời gọi API và giữ nguyên tên hàm thì Controller/View không phải sửa.
public class DemoReadingProgressStore
{
    private readonly object _lock = new();
    private readonly List<ReadingProgress> _items = [];

    // Mở chương nào thì lưu chương đó là "đọc gần nhất" của truyện (mỗi truyện một dòng)
    public void Save(string uid, int storyId, string storyTitle, int chapterNo, string chapterTitle)
    {
        if (string.IsNullOrEmpty(uid)) return;
        lock (_lock)
        {
            var p = _items.FirstOrDefault(x => x.UserId == uid && x.StoryId == storyId);
            if (p is null) { p = new ReadingProgress { UserId = uid, StoryId = storyId }; _items.Add(p); }
            p.StoryTitle = storyTitle; p.ChapterNo = chapterNo; p.ChapterTitle = chapterTitle; p.UpdatedAt = DateTime.UtcNow;
        }
    }

    public ReadingProgress? Get(string uid, int storyId)
    { lock (_lock) return _items.FirstOrDefault(x => x.UserId == uid && x.StoryId == storyId); }

    // Danh sách "Đang đọc": truyện đọc gần nhất lên đầu
    public List<ReadingProgress> List(string uid)
    { lock (_lock) return _items.Where(x => x.UserId == uid).OrderByDescending(x => x.UpdatedAt).ToList(); }

    public bool Remove(string uid, int storyId)
    { lock (_lock) return _items.RemoveAll(x => x.UserId == uid && x.StoryId == storyId) > 0; }

    public int Count(string uid) { lock (_lock) return _items.Count(x => x.UserId == uid); }
}

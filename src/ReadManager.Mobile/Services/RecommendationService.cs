using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// "ĐỀ XUẤT CHO BẠN" theo sở thích đọc
// - Sở thích (thể loại) được lưu trên máy chủ: PUT api/reader/genres, đọc lại bằng GET api/reader/profile.
//   Vì lưu theo tài khoản nên đăng xuất / đăng nhập lại (kể cả máy khác) vẫn giữ nguyên.
// - Truyện đề xuất = gom truyện của từng thể loại đã chọn (GET api/stories?genreId=), truyện khớp nhiều
//   thể loại hơn đứng trước, rồi tới truyện mới cập nhật.
public class RecommendationService
{
    private const int MaxGenres = 8;   // tránh gọi API quá nhiều khi chọn rất nhiều thể loại

    private readonly ReaderActivityService _reader;
    private readonly IStoryService _stories;

    // Bắn ra khi người dùng lưu sở thích mới -> trang chủ tải lại mục đề xuất ngay
    public static event EventHandler? PreferencesChanged;
    public static void NotifyPreferencesChanged() => PreferencesChanged?.Invoke(null, EventArgs.Empty);

    public RecommendationService(ReaderActivityService reader, IStoryService stories)
    {
        _reader = reader;
        _stories = stories;
    }

    // Id các thể loại đã chọn (rỗng nếu chưa chọn)
    public async Task<int[]> GetPreferredGenreIdsAsync()
    {
        var profile = await _reader.Profile();
        return profile?.Genres ?? Array.Empty<int>();
    }

    // Danh sách truyện đề xuất. onlyGenreId != null: chỉ lấy một thể loại (bộ lọc ở trang "Đề xuất cho bạn")
    public async Task<List<StorySummary>> GetStoriesAsync(IReadOnlyList<int> genreIds, int? onlyGenreId = null, int perGenre = 24)
    {
        var ids = onlyGenreId.HasValue ? new[] { onlyGenreId.Value } : genreIds.Take(MaxGenres).ToArray();
        var hits = new Dictionary<int, (StorySummary Story, int Count)>();

        // Gọi lần lượt (không song song) vì các lời gọi dùng chung một HttpClient
        foreach (var id in ids)
        {
            var page = await _stories.GetListAsync(null, id, "updated", null, 1, perGenre);
            foreach (var s in page.Items)
            {
                hits[s.StoryId] = hits.TryGetValue(s.StoryId, out var old) ? (old.Story, old.Count + 1) : (s, 1);
            }
        }

        return hits.Values
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Story.UpdatedAt)
            .Select(x => x.Story)
            .ToList();
    }
}

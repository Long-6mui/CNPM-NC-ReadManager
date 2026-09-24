using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services.Mocks;

public class MockChapterService : IChapterService
{
    public Task<List<ChapterSummary>> GetChaptersAsync(int storyId)
    {
        var chapters = Enumerable.Range(1, 12).Select(i => new ChapterSummary
        {
            ChapterId = storyId * 100 + i,
            StoryId = storyId,
            ChapterNumber = i,
            Title = $"Chương {i}: Khởi đầu demo",
            AccessLevel = i <= 8 ? "Free" : "Paid", // 8 chương đầu free, còn lại khoá — giống logic PB12
            PublishedAt = DateTime.Now.AddDays(-i)
        }).ToList();

        return Task.FromResult(chapters);
    }

    public Task<ChapterContent> GetChapterContentAsync(int chapterId)
    {
        var chapterNumber = chapterId % 100;
        var storyId = chapterId / 100;

        var content = new ChapterContent
        {
            ChapterId = chapterId,
            StoryId = storyId,
            ChapterNumber = chapterNumber,
            Title = $"Chương {chapterNumber}",
            Content = "Đây là nội dung demo dùng để xem giao diện màn Đọc chương. " +
                      "Khi Backend 4 hoàn thành API thật, nội dung này sẽ được thay bằng nội dung chương thật từ server.",
            AccessLevel = chapterNumber <= 8 ? "Free" : "Paid",
            PreviousChapterId = chapterNumber > 1 ? storyId * 100 + (chapterNumber - 1) : null,
            NextChapterId = chapterNumber < 12 ? storyId * 100 + (chapterNumber + 1) : null
        };
        return Task.FromResult(content);
    }
}

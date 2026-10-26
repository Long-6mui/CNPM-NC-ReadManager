using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Progress;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// PB28 — LƯU & TIẾP TỤC TIẾN ĐỘ ĐỌC
// PB29 — DANH SÁCH TRUYỆN ĐANG ĐỌC (dựa trên chính dữ liệu tiến độ của PB28)
public interface IReadingProgressService
{
    // Lưu / cập nhật điểm đọc tiếp. Trả null = truyện hoặc chương không tồn tại / chưa công khai.
    Task<ReadingProgressDto?> SaveAsync(int userId, int storyId, SaveReadingProgressDto dto);

    // Lấy điểm đọc tiếp của 1 truyện. Trả null = chưa đọc truyện này.
    Task<ReadingProgressDto?> GetAsync(int userId, int storyId);

    // PB29 — danh sách truyện đang đọc, mới đọc gần nhất xếp trước
    Task<PagedResultDto<ReadingStoryDto>> GetReadingListAsync(int userId, int page, int pageSize);

    // Bỏ truyện khỏi danh sách đang đọc. Trả false = vốn không có trong danh sách.
    Task<bool> RemoveAsync(int userId, int storyId);
}

public class ReadingProgressService : IReadingProgressService
{
    private const string Published = "Published";
    private const string PublicVisibility = "Public";
    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;

    public ReadingProgressService(AppDbContext db)
    {
        _db = db;
    }

    // ===== PB28: LƯU TIẾN ĐỘ =====
    public async Task<ReadingProgressDto?> SaveAsync(int userId, int storyId, SaveReadingProgressDto dto)
    {
        if (dto.ScrollPercent < 0 || dto.ScrollPercent > 100)
            throw new ArgumentException("Vị trí đọc phải từ 0 đến 100 (%).");

        // 1. Chương phải thuộc truyện này, truyện đã công khai, chương đã ra mắt
        //    (cùng điều kiện với bình luận — CommentService)
        var now = DateTime.UtcNow;
        var chapter = await _db.Chapters.AsNoTracking()
            .Where(c => c.ChapterId == dto.ChapterId
                     && c.StoryId == storyId
                     && c.PublicationStatus == Published
                     && (c.ScheduledAt == null || c.ScheduledAt <= now)
                     && c.Story.Visibility == PublicVisibility)
            .Select(c => new { c.ChapterNumber, c.Title })
            .FirstOrDefaultAsync();
        if (chapter is null)
            return null;

        var percent = Math.Round(dto.ScrollPercent, 2);

        // 2. Chưa có thì thêm mới, có rồi thì ghi đè (người dùng chỉ có 1 điểm đọc tiếp / truyện)
        var progress = await _db.ReadingProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.StoryId == storyId);

        if (progress is null)
        {
            progress = new ReadingProgress { UserId = userId, StoryId = storyId };
            Apply(progress, dto.ChapterId, percent, now);
            _db.ReadingProgresses.Add(progress);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 2 thiết bị lưu cùng lúc → người kia vừa tạo dòng này (trùng khóa chính).
                // Bỏ bản của mình, lấy dòng đã có rồi cập nhật.
                _db.Entry(progress).State = EntityState.Detached;
                progress = await _db.ReadingProgresses
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.StoryId == storyId);
                if (progress is null) throw;   // không phải lỗi trùng khóa → báo lỗi thật
                Apply(progress, dto.ChapterId, percent, now);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            Apply(progress, dto.ChapterId, percent, now);
            await _db.SaveChangesAsync();
        }

        return new ReadingProgressDto
        {
            StoryId = storyId,
            ChapterId = dto.ChapterId,
            ChapterNumber = chapter.ChapterNumber,
            ChapterTitle = chapter.Title,
            ScrollPercent = percent,
            UpdatedAt = now
        };
    }

    // ===== PB28: TIẾP TỤC ĐỌC =====
    public async Task<ReadingProgressDto?> GetAsync(int userId, int storyId)
        => await _db.ReadingProgresses.AsNoTracking()
            .Where(p => p.UserId == userId && p.StoryId == storyId
                     && p.Story.Visibility == PublicVisibility)
            .Select(p => new ReadingProgressDto
            {
                StoryId = p.StoryId,
                ChapterId = p.ChapterId,
                ChapterNumber = p.Chapter.ChapterNumber,
                ChapterTitle = p.Chapter.Title,
                ScrollPercent = p.ScrollPercent,
                UpdatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();

    // ===== PB29: DANH SÁCH TRUYỆN ĐANG ĐỌC =====
    public async Task<PagedResultDto<ReadingStoryDto>> GetReadingListAsync(int userId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var now = DateTime.UtcNow;

        // Truyện đã bị ẩn (không còn công khai) thì không hiện trong danh sách
        var query = _db.ReadingProgresses.AsNoTracking()
            .Where(p => p.UserId == userId && p.Story.Visibility == PublicVisibility);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ReadingStoryDto
            {
                StoryId = p.StoryId,
                Title = p.Story.Title,
                AuthorName = p.Story.AuthorName,
                CoverUrl = p.Story.CoverUrl,
                ChapterId = p.ChapterId,
                ChapterNumber = p.Chapter.ChapterNumber,
                ChapterTitle = p.Chapter.Title,
                ScrollPercent = p.ScrollPercent,
                LastReadAt = p.UpdatedAt,
                PublishedChapterCount = _db.Chapters.Count(c =>
                    c.StoryId == p.StoryId
                    && c.PublicationStatus == Published
                    && (c.ScheduledAt == null || c.ScheduledAt <= now)),
                HasNewChapters = _db.Chapters.Any(c =>
                    c.StoryId == p.StoryId
                    && c.PublicationStatus == Published
                    && (c.ScheduledAt == null || c.ScheduledAt <= now)
                    && c.ChapterNumber > p.Chapter.ChapterNumber)
            })
            .ToListAsync();

        return new PagedResultDto<ReadingStoryDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ===== BỎ KHỎI DANH SÁCH ĐANG ĐỌC =====
    public async Task<bool> RemoveAsync(int userId, int storyId)
        => await _db.ReadingProgresses
            .Where(p => p.UserId == userId && p.StoryId == storyId)
            .ExecuteDeleteAsync() > 0;

    // ===== HÀM PHỤ =====
    private static void Apply(ReadingProgress progress, int chapterId, decimal percent, DateTime now)
    {
        progress.ChapterId = chapterId;
        progress.ScrollPercent = percent;
        progress.UpdatedAt = now;
    }
}

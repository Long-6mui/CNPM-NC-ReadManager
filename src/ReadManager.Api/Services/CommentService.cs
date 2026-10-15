using System.Text;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// Kết quả khi xóa bình luận
public enum DeleteCommentResult
{
    Deleted,     // xóa thành công
    NotFound,    // không có bình luận này
    Forbidden    // không phải người viết, cũng không phải admin
}

// PB32 — BÌNH LUẬN
public interface ICommentService
{
    // Danh sách bình luận của truyện (chapterId = null → tất cả bình luận của truyện)
    // Trả null = truyện không tồn tại / chưa công khai.
    Task<PagedResultDto<CommentDto>?> GetListAsync(int storyId, int? chapterId, int page, int pageSize,
                                                   int? currentUserId, bool isAdmin);

    // Đăng bình luận. Trả null = truyện không tồn tại / chưa công khai.
    // Ném ArgumentException = dữ liệu sai (Controller bắt → 400).
    Task<CommentDto?> CreateAsync(int storyId, int userId, CreateCommentDto dto);

    // Xóa bình luận (người viết hoặc admin)
    Task<DeleteCommentResult> DeleteAsync(int commentId, int userId, bool isAdmin);
}

public class CommentService : ICommentService
{
    private const string Visible = "Visible";
    private const string Hidden = "Hidden";
    private const string Published = "Published";
    private const string PublicVisibility = "Public";
    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;

    public CommentService(AppDbContext db)
    {
        _db = db;
    }

    // ----- XEM BÌNH LUẬN -----
    public async Task<PagedResultDto<CommentDto>?> GetListAsync(int storyId, int? chapterId, int page, int pageSize,
                                                                int? currentUserId, bool isAdmin)
    {
        // 1. Truyện phải đang công khai
        var storyOk = await _db.Stories.AnyAsync(s => s.StoryId == storyId && s.Visibility == PublicVisibility);
        if (!storyOk)
            return null;

        // 2. Giới hạn trang hợp lệ
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // 3. Chỉ lấy bình luận đang hiện
        var query = _db.Comments.AsNoTracking()
            .Where(c => c.StoryId == storyId && c.Status == Visible);
        if (chapterId.HasValue)
            query = query.Where(c => c.ChapterId == chapterId.Value);

        var total = await query.CountAsync();

        // 4. Mới nhất ở trên, lấy kèm tên người viết + số chương
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.CommentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommentDto
            {
                CommentId = c.CommentId,
                StoryId = c.StoryId,
                ChapterId = c.ChapterId,
                ChapterNumber = c.Chapter != null ? c.Chapter.ChapterNumber : null,
                UserId = c.UserId,
                DisplayName = c.User.DisplayName,
                AvatarUrl = c.User.AvatarUrl,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                CanDelete = isAdmin || (currentUserId.HasValue && c.UserId == currentUserId.Value)
            })
            .ToListAsync();

        return new PagedResultDto<CommentDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ----- ĐĂNG BÌNH LUẬN -----
    public async Task<CommentDto?> CreateAsync(int storyId, int userId, CreateCommentDto dto)
    {
        // 1. Truyện phải đang công khai
        var storyOk = await _db.Stories.AnyAsync(s => s.StoryId == storyId && s.Visibility == PublicVisibility);
        if (!storyOk)
            return null;

        // 2. Chuẩn hóa nội dung: sửa dấu tiếng Việt (NFC), bỏ khoảng trắng 2 đầu
        var content = (dto.Content ?? string.Empty)
            .Replace("\r\n", "\n")
            .Normalize(NormalizationForm.FormC)
            .Trim();
        if (content.Length == 0)
            throw new ArgumentException("Vui lòng nhập nội dung bình luận.");
        if (content.Length > 1000)
            throw new ArgumentException("Bình luận tối đa 1000 ký tự.");

        // 3. Nếu bình luận cho 1 chương → chương phải thuộc truyện này, đã công khai và đã tới giờ ra mắt
        int? chapterNumber = null;
        if (dto.ChapterId.HasValue)
        {
            var now = DateTime.UtcNow;
            var chapter = await _db.Chapters.AsNoTracking()
                .Where(c => c.ChapterId == dto.ChapterId.Value
                         && c.StoryId == storyId
                         && c.PublicationStatus == Published
                         && (c.ScheduledAt == null || c.ScheduledAt <= now))
                .Select(c => new { c.ChapterNumber })
                .FirstOrDefaultAsync();
            if (chapter is null)
                throw new ArgumentException("Chương không tồn tại hoặc chưa ra mắt.");
            chapterNumber = chapter.ChapterNumber;
        }

        // 4. Lưu
        var comment = new Comment
        {
            UserId = userId,
            StoryId = storyId,
            ChapterId = dto.ChapterId,
            Content = content,
            Status = Visible,
            CreatedAt = DateTime.UtcNow
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // 5. Trả lại bình luận vừa đăng (kèm tên người viết) để giao diện hiện ngay
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.DisplayName, u.AvatarUrl })
            .FirstAsync();

        return new CommentDto
        {
            CommentId = comment.CommentId,
            StoryId = storyId,
            ChapterId = comment.ChapterId,
            ChapterNumber = chapterNumber,
            UserId = userId,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            CanDelete = true
        };
    }

    // ----- XÓA BÌNH LUẬN -----
    // Không xóa hẳn khỏi database mà chuyển sang "Hidden" → báo cáo vi phạm (PB33) vẫn tra lại được.
    public async Task<DeleteCommentResult> DeleteAsync(int commentId, int userId, bool isAdmin)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId && c.Status == Visible);
        if (comment is null)
            return DeleteCommentResult.NotFound;

        if (!isAdmin && comment.UserId != userId)
            return DeleteCommentResult.Forbidden;

        comment.Status = Hidden;
        await _db.SaveChangesAsync();
        return DeleteCommentResult.Deleted;
    }
}

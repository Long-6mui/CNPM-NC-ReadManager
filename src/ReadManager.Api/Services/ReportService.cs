using System.Text;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// PB33 — BÁO CÁO VI PHẠM
public interface IReportService
{
    List<string> GetReasons();

    // Độc giả gửi báo cáo. Trả null = đối tượng bị báo cáo không tồn tại.
    // Ném ArgumentException (→ 400) hoặc InvalidOperationException (→ 409: đã báo cáo rồi).
    Task<ReportDto?> CreateAsync(int reporterId, CreateReportDto dto);

    // Admin xem danh sách (status = null → tất cả)
    Task<PagedResultDto<ReportDto>> GetListAsync(string? status, int page, int pageSize);

    // Admin xử lý. Trả null = không tìm thấy. Ném InvalidOperationException (→ 409) nếu đã xử lý rồi.
    Task<ReportDto?> HandleAsync(int reportId, HandleReportDto dto);
}

public class ReportService : IReportService
{
    private const string Pending = "Pending";
    private const string Resolved = "Resolved";
    private const string Visible = "Visible";
    private const string Hidden = "Hidden";
    private const string Published = "Published";
    private const string PublicVisibility = "Public";
    private const int MaxPageSize = 50;

    // Các lý do cho độc giả chọn (Web/Mobile lấy qua GET api/reports/reasons)
    private static readonly List<string> Reasons = new()
    {
        "Spam / quảng cáo",
        "Nội dung phản cảm, thô tục",
        "Xúc phạm, công kích người khác",
        "Vi phạm bản quyền",
        "Tiết lộ nội dung (spoil)",
        "Khác"
    };

    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public List<string> GetReasons() => Reasons.ToList();

    // ----- ĐỘC GIẢ GỬI BÁO CÁO -----
    public async Task<ReportDto?> CreateAsync(int reporterId, CreateReportDto dto)
    {
        // 1. Lý do phải nằm trong danh sách (chuẩn hóa dấu tiếng Việt trước khi so)
        var reason = dto.Reason.Normalize(NormalizationForm.FormC).Trim();
        if (!Reasons.Contains(reason))
            throw new ArgumentException("Lý do báo cáo không hợp lệ.");

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Normalize(NormalizationForm.FormC).Trim();
        if (reason == "Khác" && note is null)
            throw new ArgumentException("Chọn lý do \"Khác\" thì vui lòng ghi rõ lý do.");

        // 2. Đối tượng bị báo cáo phải tồn tại và độc giả đang nhìn thấy được
        if (!await TargetVisibleAsync(dto.TargetType, dto.TargetId))
            return null;

        // 3. Chặn báo cáo trùng: 1 người chỉ có 1 báo cáo "chờ xử lý" cho cùng 1 đối tượng
        var duplicate = await _db.Reports.AnyAsync(r =>
            r.ReporterId == reporterId && r.TargetType == dto.TargetType
            && r.TargetId == dto.TargetId && r.Status == Pending);
        if (duplicate)
            throw new InvalidOperationException("Bạn đã báo cáo nội dung này rồi, Admin đang xem xét.");

        // 4. Lưu
        var report = new Report
        {
            ReporterId = reporterId,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            Reason = reason,
            Note = note,
            Status = Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        return (await ToDtosAsync(new List<Report> { report }))[0];
    }

    // ----- ADMIN XEM DANH SÁCH -----
    public async Task<PagedResultDto<ReportDto>> GetListAsync(string? status, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Reports.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.ReportId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<ReportDto>
        {
            Items = await ToDtosAsync(rows),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ----- ADMIN XỬ LÝ -----
    public async Task<ReportDto?> HandleAsync(int reportId, HandleReportDto dto)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.ReportId == reportId);
        if (report is null)
            return null;
        if (report.Status != Pending)
            throw new InvalidOperationException("Báo cáo này đã được xử lý rồi.");

        var now = DateTime.UtcNow;
        report.Status = dto.Status;
        report.HandledAt = now;

        if (dto.Status == Resolved)
        {
            // Vi phạm thật → các báo cáo khác đang chờ về CÙNG đối tượng cũng coi như đã xử lý
            var others = await _db.Reports
                .Where(r => r.ReportId != reportId && r.Status == Pending
                         && r.TargetType == report.TargetType && r.TargetId == report.TargetId)
                .ToListAsync();
            foreach (var r in others)
            {
                r.Status = Resolved;
                r.HandledAt = now;
            }

            // Báo cáo bình luận → ẩn bình luận nếu Admin chọn
            if (dto.HideComment && report.TargetType == "Comment")
            {
                var comment = await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == report.TargetId);
                if (comment is not null)
                    comment.Status = Hidden;
            }
        }

        await _db.SaveChangesAsync();
        return (await ToDtosAsync(new List<Report> { report }))[0];
    }

    // ===== HÀM PHỤ =====

    // Đối tượng có tồn tại và đang hiện cho độc giả không
    private async Task<bool> TargetVisibleAsync(string type, int id)
    {
        var now = DateTime.UtcNow;
        return type switch
        {
            "Story" => await _db.Stories.AnyAsync(s => s.StoryId == id && s.Visibility == PublicVisibility),
            "Chapter" => await _db.Chapters.AnyAsync(c => c.ChapterId == id
                            && c.PublicationStatus == Published
                            && (c.ScheduledAt == null || c.ScheduledAt <= now)
                            && c.Story.Visibility == PublicVisibility),
            "Comment" => await _db.Comments.AnyAsync(c => c.CommentId == id && c.Status == Visible),
            _ => false
        };
    }

    // Đổi Report → ReportDto, kèm tên người báo cáo + mô tả ngắn đối tượng bị báo cáo
    // (tra theo từng nhóm Story / Chapter / Comment để không phải gọi database cho từng dòng)
    private async Task<List<ReportDto>> ToDtosAsync(List<Report> reports)
    {
        var userIds = reports.Select(r => r.ReporterId).Distinct().ToList();
        var storyIds = reports.Where(r => r.TargetType == "Story").Select(r => r.TargetId).Distinct().ToList();
        var chapterIds = reports.Where(r => r.TargetType == "Chapter").Select(r => r.TargetId).Distinct().ToList();
        var commentIds = reports.Where(r => r.TargetType == "Comment").Select(r => r.TargetId).Distinct().ToList();

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.DisplayName);

        var stories = await _db.Stories.AsNoTracking()
            .Where(s => storyIds.Contains(s.StoryId))
            .ToDictionaryAsync(s => s.StoryId, s => s.Title);

        var chapters = await _db.Chapters.AsNoTracking()
            .Where(c => chapterIds.Contains(c.ChapterId))
            .Select(c => new { c.ChapterId, c.StoryId, c.ChapterNumber, c.Title, StoryTitle = c.Story.Title })
            .ToDictionaryAsync(c => c.ChapterId);

        var comments = await _db.Comments.AsNoTracking()
            .Where(c => commentIds.Contains(c.CommentId))
            .Select(c => new
            {
                c.CommentId, c.StoryId, c.Content, c.Status,
                ChapterNumber = c.Chapter != null ? c.Chapter.ChapterNumber : (int?)null,
                Author = c.User.DisplayName
            })
            .ToDictionaryAsync(c => c.CommentId);

        var list = new List<ReportDto>();
        foreach (var r in reports)
        {
            var dto = new ReportDto
            {
                ReportId = r.ReportId,
                ReporterId = r.ReporterId,
                ReporterName = users.GetValueOrDefault(r.ReporterId, "(không rõ)"),
                TargetType = r.TargetType,
                TargetId = r.TargetId,
                Reason = r.Reason,
                Note = r.Note,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                HandledAt = r.HandledAt,
                TargetPreview = "(đã bị xóa)"
            };

            if (r.TargetType == "Story" && stories.TryGetValue(r.TargetId, out var storyTitle))
            {
                dto.StoryId = r.TargetId;
                dto.TargetPreview = $"Truyện: {storyTitle}";
                dto.TargetExists = true;
            }
            else if (r.TargetType == "Chapter" && chapters.TryGetValue(r.TargetId, out var ch))
            {
                dto.StoryId = ch.StoryId;
                dto.ChapterNumber = ch.ChapterNumber;
                dto.TargetPreview = $"{ch.StoryTitle} — Chương {ch.ChapterNumber}: {ch.Title}";
                dto.TargetExists = true;
            }
            else if (r.TargetType == "Comment" && comments.TryGetValue(r.TargetId, out var cm))
            {
                dto.StoryId = cm.StoryId;
                dto.ChapterNumber = cm.ChapterNumber;
                var text = cm.Content.Length <= 150 ? cm.Content : cm.Content[..150] + "…";
                dto.TargetPreview = $"{cm.Author}: \"{text}\"" + (cm.Status == Hidden ? " (đã ẩn)" : "");
                dto.TargetExists = cm.Status == Visible;
            }

            list.Add(dto);
        }
        return list;
    }
}

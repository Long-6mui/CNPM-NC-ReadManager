using System.Text;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Chapters;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// INTERFACE — "bản hợp đồng" liệt kê các chức năng mà Service cung cấp.
// Controller chỉ biết interface này, không cần biết bên trong làm sao.
public interface IChapterService
{
    // ----- Phần ĐỌC (độc giả + admin) -----
    Task<List<ChapterListItemDto>?> GetListAsync(int storyId, bool includeDrafts);              // PB11
    Task<ChapterReadDto?> GetByIdAsync(int chapterId, bool isAdmin);                            // PB12 (Mobile)
    Task<ChapterReadDto?> GetByNumberAsync(int storyId, int chapterNumber, bool isAdmin);       // PB12 (Web)

    // ----- Phần GHI (chỉ admin) -----
    Task<ChapterListItemDto?> CreateAsync(int storyId, CreateChapterDto dto);                   // PB07
    Task<ChapterListItemDto?> UpdateAsync(int chapterId, UpdateChapterDto dto);                 // PB08
    Task<bool> DeleteAsync(int chapterId);
    Task<UploadChaptersResultDto?> UploadChaptersAsync(int storyId, UploadChaptersDto dto);     // PB08 (JSON)
    Task<UploadChapterFilesResultDto?> UploadChapterFilesAsync(int storyId, UploadChapterFilesDto dto); // PB08 (file)
}

// CLASS XỬ LÝ CHÍNH — làm thật các chức năng đã liệt kê ở interface.
public class ChapterService : IChapterService
{
    // Các giá trị chữ dùng nhiều lần → đặt hằng số để khỏi gõ sai chính tả.
    private const string Published = "Published";
    private const string Free = "Free";
    private const string PublicVisibility = "Public";

    // Giới hạn giống trong CreateChapterDto
    private const int MaxChapterNumber = 100000;
    private const int MaxTitleLength = 255;
    private const int MaxContentLength = 200000;
    private const int MaxChaptersPerUpload = 500;

    // _db = cổng kết nối database (EF Core), được ASP.NET tự truyền vào.
    private readonly AppDbContext _db;

    public ChapterService(AppDbContext db)
    {
        _db = db;
    }

    //  PHẦN 1: ĐỌC DỮ LIỆU

    // PB11 — LẤY DANH SÁCH CHƯƠNG TRUYỆN

    public async Task<List<ChapterListItemDto>?> GetListAsync(int storyId, bool includeDrafts)
    {
        // 1. Tìm truyện
        var story = await _db.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.StoryId == storyId);
        if (story is null || (!includeDrafts && story.Visibility != PublicVisibility))
            return null;

        // 2. Lấy các chương của truyện đó
        var query = _db.Chapters.AsNoTracking().Where(c => c.StoryId == storyId);
        if (!includeDrafts)
            query = query.Where(c => c.PublicationStatus == Published);   // độc giả chỉ thấy chương đã công khai

        // 3. Sắp xếp theo số chương tăng dần, chuyển sang DTO
        var rows = await query
            .OrderBy(c => c.ChapterNumber)
            .Select(c => new ChapterListItemDto
            {
                ChapterId = c.ChapterId,
                StoryId = c.StoryId,
                ChapterNumber = c.ChapterNumber,
                Title = c.Title,
                AccessLevel = c.AccessLevel,
                PublicationStatus = c.PublicationStatus,
                PublishedAt = c.PublishedAt,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        // 4. PB10 — với độc giả: đánh dấu chương nào bị khóa (hiện 🔒)
        if (!includeDrafts)
        {
            foreach (var row in rows)
                row.IsLocked = IsLocked(story.AccessPolicy, row.AccessLevel);
        }

        // Sửa luôn dấu tiếng Việt cho các chương đã lưu từ trước
        foreach (var row in rows)
            row.Title = NormalizeTitle(row.Title);

        return rows;
    }

    // PB12 — ĐỌC CHƯƠNG THEO ID (Mobile dùng)

    public async Task<ChapterReadDto?> GetByIdAsync(int chapterId, bool isAdmin)
    {
        var chapter = await _db.Chapters.AsNoTracking()
            .Include(c => c.Story)                         // lấy kèm thông tin truyện
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);

        return chapter is null ? null : await BuildReadDtoAsync(chapter, isAdmin);
    }


    // PB12 — ĐỌC CHƯƠNG THEO SỐ CHƯƠNG (Web dùng) vd: truyện 1, chương 3

    public async Task<ChapterReadDto?> GetByNumberAsync(int storyId, int chapterNumber, bool isAdmin)
    {
        var chapter = await _db.Chapters.AsNoTracking()
            .Include(c => c.Story)
            .FirstOrDefaultAsync(c => c.StoryId == storyId && c.ChapterNumber == chapterNumber);

        return chapter is null ? null : await BuildReadDtoAsync(chapter, isAdmin);
    }

  
    // HÀM PHỤ: dựng dữ liệu cho màn hình đọc (dùng chung cho 2 hàm trên)
 
    private async Task<ChapterReadDto?> BuildReadDtoAsync(Chapter chapter, bool isAdmin)
    {
        // 1. Độc giả chỉ được xem chương ĐÃ CÔNG KHAI của truyện ĐÃ CÔNG KHAI.
        //    Không đạt → trả null (404), không cho biết chương có tồn tại hay không.
        //    Admin thì bỏ qua bước này (để xem trước chương nháp).
        if (!isAdmin && (chapter.Story.Visibility != PublicVisibility || chapter.PublicationStatus != Published))
            return null;

        // 2. PB10 — KIỂM TRA QUYỀN ĐỌC MIỄN PHÍ. Admin luôn đọc được.
        var locked = !isAdmin && IsLocked(chapter.Story.AccessPolicy, chapter.AccessLevel);

        // 3. Tìm chương TRƯỚC và chương SAU để làm nút chuyển chương.
        var siblings = _db.Chapters.AsNoTracking().Where(c => c.StoryId == chapter.StoryId);
        if (!isAdmin)
            siblings = siblings.Where(c => c.PublicationStatus == Published);   // độc giả không nhảy vào chương nháp

        var prev = await siblings
            .Where(c => c.ChapterNumber < chapter.ChapterNumber)
            .OrderByDescending(c => c.ChapterNumber)        // chương nhỏ hơn GẦN NHẤT
            .Select(c => new { c.ChapterId, c.ChapterNumber })
            .FirstOrDefaultAsync();

        var next = await siblings
            .Where(c => c.ChapterNumber > chapter.ChapterNumber)
            .OrderBy(c => c.ChapterNumber)                  // chương lớn hơn GẦN NHẤT
            .Select(c => new { c.ChapterId, c.ChapterNumber })
            .FirstOrDefaultAsync();

        // 4. Gói lại thành DTO trả về
        return new ChapterReadDto
        {
            ChapterId = chapter.ChapterId,
            StoryId = chapter.StoryId,
            StoryTitle = NormalizeTitle(chapter.Story.Title),
            ChapterNumber = chapter.ChapterNumber,
            Title = NormalizeTitle(chapter.Title),                                  // sửa dấu cho dữ liệu cũ
            Content = locked ? string.Empty : NormalizeContent(chapter.Content),   // BỊ KHÓA → KHÔNG trả nội dung
            AccessLevel = chapter.AccessLevel,
            PublicationStatus = chapter.PublicationStatus,
            IsLocked = locked,
            PreviousChapterId = prev?.ChapterId,                 // "?." = nếu prev là null thì kết quả cũng null
            PreviousChapterNumber = prev?.ChapterNumber,
            NextChapterId = next?.ChapterId,
            NextChapterNumber = next?.ChapterNumber,
            PublishedAt = chapter.PublishedAt,
            UpdatedAt = chapter.UpdatedAt
        };
    }

    //    PHẦN 2: GHI DỮ LIỆU (chỉ admin)

    // PB07 — TẠO CHƯƠNG MỚI
    // Trả null = không tìm thấy truyện.
    // Ném lỗi InvalidOperationException = trùng số chương (Controller bắt → 409).
  
    public async Task<ChapterListItemDto?> CreateAsync(int storyId, CreateChapterDto dto)
    {
        // 1. Truyện phải tồn tại
        var story = await _db.Stories.FindAsync(storyId);
        if (story is null)
            return null;

        // 2. Số chương không được trùng với chương đã có
        await EnsureNumberFreeAsync(storyId, dto.ChapterNumber, exceptChapterId: null);

        // 3. Tạo object chương mới
        var now = DateTime.UtcNow;
        var chapter = new Chapter
        {
            StoryId = storyId,
            ChapterNumber = dto.ChapterNumber,
            Title = NormalizeTitle(dto.Title),                                // Trim = bỏ khoảng trắng thừa 2 đầu
            Content = NormalizeContent(dto.Content),
            AccessLevel = ResolveAccessLevel(story, dto.AccessLevel),
            PublicationStatus = dto.PublicationStatus,
            PublishedAt = dto.PublicationStatus == Published ? now : null,   // công khai ngay thì ghi ngày
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Chapters.Add(chapter);

        // 4. Có chương mới công khai → cập nhật ngày của truyện
        //    để truyện nổi lên mục "Mới cập nhật" ở trang chủ.
        if (chapter.PublicationStatus == Published)
            story.UpdatedAt = now;

        // 5. Lưu vào database
        await SaveOrThrowDuplicateAsync(dto.ChapterNumber);
        return ToListItem(chapter);
    }

    
    // PB08 — SỬA CHƯƠNG (đổi được cả số chương)

    public async Task<ChapterListItemDto?> UpdateAsync(int chapterId, UpdateChapterDto dto)
    {
        // 1. Lấy chương hiện tại
        var current = await _db.Chapters.AsNoTracking().FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        if (current is null)
            return null;

        var story = await _db.Stories.FindAsync(current.StoryId);
        if (story is null)
            return null;

        // 2. Nếu đổi số chương → kiểm tra số mới có bị trùng không
        if (dto.ChapterNumber != current.ChapterNumber)
            await EnsureNumberFreeAsync(current.StoryId, dto.ChapterNumber, exceptChapterId: chapterId);

        // 3. Chuẩn bị giá trị mới
        var now = DateTime.UtcNow;
        var number = dto.ChapterNumber;
        var title = NormalizeTitle(dto.Title);
        var content = NormalizeContent(dto.Content);
        var access = ResolveAccessLevel(story, dto.AccessLevel);
        var status = dto.PublicationStatus;
        var publishedAt = status == Published ? current.PublishedAt ?? now : current.PublishedAt;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
         
            await _db.Chapters
                .Where(c => c.ChapterId == chapterId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.ChapterNumber, number)
                    .SetProperty(c => c.Title, title)
                    .SetProperty(c => c.Content, content)
                    .SetProperty(c => c.AccessLevel, access)
                    .SetProperty(c => c.PublicationStatus, status)
                    .SetProperty(c => c.PublishedAt, publishedAt)
                    .SetProperty(c => c.UpdatedAt, now));
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            throw DuplicateNumber(number);
        }

        // Chương liên quan tới phần công khai → cập nhật ngày của truyện
        if (status == Published || current.PublicationStatus == Published)
        {
            story.UpdatedAt = now;
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();   // xác nhận lưu

        // 5. Đọc lại chương sau khi sửa để trả về
        var updated = await _db.Chapters.AsNoTracking().FirstAsync(c => c.ChapterId == chapterId);
        return ToListItem(updated);
    }

    // -----------------------------------------------------------------
    // XÓA CHƯƠNG — trả false nếu không tìm thấy.
    // -----------------------------------------------------------------
    public async Task<bool> DeleteAsync(int chapterId)
    {
        var chapter = await _db.Chapters.FindAsync(chapterId);
        if (chapter is null)
            return false;

        // Xóa chương đang công khai → cập nhật ngày của truyện
        if (chapter.PublicationStatus == Published)
        {
            var story = await _db.Stories.FindAsync(chapter.StoryId);
            if (story is not null)
                story.UpdatedAt = DateTime.UtcNow;
        }

        _db.Chapters.Remove(chapter);
        await _db.SaveChangesAsync();
        return true;
    }

    // -----------------------------------------------------------------
    // PB08 — TẢI LÊN NHIỀU CHƯƠNG CÙNG LÚC (dạng JSON, đã tách sẵn)
    // Tất cả được lưu trong 1 lần SaveChanges:
    // 1 chương lỗi → KHÔNG chương nào được lưu (tránh lưu nửa chừng).
    // -----------------------------------------------------------------
    public async Task<UploadChaptersResultDto?> UploadChaptersAsync(int storyId, UploadChaptersDto dto)
    {
        // 1. Truyện phải tồn tại
        var story = await _db.Stories.FindAsync(storyId);
        if (story is null)
            return null;

        // 2. Trong dữ liệu gửi lên không được có 2 chương cùng số
        var repeated = dto.Chapters
            .GroupBy(c => c.ChapterNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(n => n)
            .ToList();
        if (repeated.Count > 0)
            throw new InvalidOperationException(
                $"Số chương bị lặp trong nội dung gửi lên: {string.Join(", ", repeated)}.");

        // 3. Tìm các chương ĐÃ CÓ trong database trùng số với chương gửi lên
        var numbers = dto.Chapters.Select(c => c.ChapterNumber).ToList();
        var existing = await _db.Chapters
            .Where(c => c.StoryId == storyId && numbers.Contains(c.ChapterNumber))
            .ToDictionaryAsync(c => c.ChapterNumber);   // tra cứu nhanh theo số chương

        var now = DateTime.UtcNow;
        var result = new UploadChaptersResultDto();
        var touchStory = false;   // có cần cập nhật ngày của truyện không

        // 4. Duyệt từng chương gửi lên
        foreach (var item in dto.Chapters.OrderBy(c => c.ChapterNumber))
        {
            var access = ResolveAccessLevel(story, item.AccessLevel);

            if (existing.TryGetValue(item.ChapterNumber, out var chapter))
            {
                // ----- Chương này ĐÃ CÓ -----
                if (!dto.OverwriteExisting)
                {
                    result.SkippedNumbers.Add(item.ChapterNumber);   // không ghi đè → bỏ qua
                    continue;
                }

                if (chapter.PublicationStatus == Published)
                    touchStory = true;

                // Ghi đè nội dung mới
                chapter.Title = NormalizeTitle(item.Title);
                chapter.Content = NormalizeContent(item.Content);
                chapter.AccessLevel = access;
                if (item.PublicationStatus == Published && chapter.PublishedAt is null)
                    chapter.PublishedAt = now;
                chapter.PublicationStatus = item.PublicationStatus;
                chapter.UpdatedAt = now;
                result.Updated++;
            }
            else
            {
                // ----- Chương MỚI → thêm vào -----
                _db.Chapters.Add(new Chapter
                {
                    StoryId = storyId,
                    ChapterNumber = item.ChapterNumber,
                    Title = NormalizeTitle(item.Title),
                    Content = NormalizeContent(item.Content),
                    AccessLevel = access,
                    PublicationStatus = item.PublicationStatus,
                    PublishedAt = item.PublicationStatus == Published ? now : null,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                result.Created++;
            }

            if (item.PublicationStatus == Published)
                touchStory = true;
        }

        if (touchStory)
            story.UpdatedAt = now;

        // 5. Lưu tất cả 1 lần
        await SaveOrThrowDuplicateAsync(null);

        // 6. Tạo câu thông báo kết quả
        result.Message = $"Đã thêm {result.Created} chương, cập nhật {result.Updated} chương"
            + (result.SkippedNumbers.Count > 0
                ? $", bỏ qua {result.SkippedNumbers.Count} chương đã có (số {string.Join(", ", result.SkippedNumbers)})."
                : ".");
        return result;
    }

    // PB08 — TẢI CHƯƠNG LÊN BẰNG FILE .txt / .zip / VĂN BẢN DÁN
    //
    // Bước 1: ChapterFileReader đọc file, tách thành từng chương.
    // Bước 2: Kiểm tra từng chương → ghi lỗi / cảnh báo để hiện lên màn hình.
    // Bước 3: Nếu dto.Save = true VÀ không có lỗi → gọi UploadChaptersAsync để lưu.

    public async Task<UploadChapterFilesResultDto?> UploadChapterFilesAsync(int storyId, UploadChapterFilesDto dto)
    {
        var story = await _db.Stories.FindAsync(storyId);
        if (story is null)
            return null;

        var result = new UploadChapterFilesResultDto();

        // ---------- Chưa chọn gì ----------
        if (dto.Files.Count == 0 && string.IsNullOrWhiteSpace(dto.Text))
        {
            result.Errors.Add("Chưa chọn file hoặc dán nội dung.");
            result.Message = "Chưa có gì để kiểm tra.";
            return result;
        }

        // ---------- Bước 1: đọc & tách chương ----------
        var read = await ChapterFileReader.ReadAsync(dto.Text, dto.Files);
        result.Errors.AddRange(read.Errors);
        result.Warnings.AddRange(read.Warnings);
        var chapters = read.Chapters;

        if (chapters.Count == 0)
        {
            if (result.Errors.Count == 0)
                result.Errors.Add("Không tách được chương nào. Mỗi chương cần bắt đầu bằng dòng \"Chương 1: Tên chương\".");
            result.Message = "Không đọc được chương nào.";
            return result;
        }
        if (chapters.Count > MaxChaptersPerUpload)
            result.Errors.Add($"Mỗi lần chỉ tải tối đa {MaxChaptersPerUpload} chương (đang có {chapters.Count}). Hãy chia nhỏ file.");

        // ---------- Bước 2: kiểm tra từng chương ----------
        // Các số chương truyện ĐÃ CÓ trong database
        var existingNumbers = (await _db.Chapters
                .Where(c => c.StoryId == storyId)
                .Select(c => c.ChapterNumber)
                .ToListAsync())
            .ToHashSet();

        // 2a. Chương chưa có số → tự đánh số tiếp theo (sau số lớn nhất đang có)
        var nextNumber = existingNumbers
            .Concat(chapters.Where(c => c.ChapterNumber.HasValue).Select(c => c.ChapterNumber!.Value))
            .DefaultIfEmpty(0)
            .Max() + 1;

        foreach (var c in chapters.Where(c => c.ChapterNumber is null))
        {
            c.ChapterNumber = nextNumber++;
            if (string.IsNullOrEmpty(c.NumberFrom))
            {
                c.NumberFrom = "Tự đánh số";
                c.Problems.Add($"Không tìm thấy số chương trong tên file hay nội dung → tự đánh số {c.ChapterNumber}. Hãy kiểm tra lại thứ tự.");
            }
        }

        // 2b. Kiểm tra tiêu đề, nội dung, số chương
        foreach (var c in chapters)
        {
            var no = c.ChapterNumber!.Value;

            if (no > MaxChapterNumber)
            {
                c.HasError = true;
                c.Problems.Add($"Số chương {no} quá lớn (tối đa {MaxChapterNumber}).");
            }

            if (string.IsNullOrWhiteSpace(c.Title))
            {
                c.Title = $"Chương {no}";
                c.Problems.Add($"Chương không có tiêu đề → tự đặt là \"Chương {no}\".");
            }
            else if (c.Title.Length > MaxTitleLength)
            {
                c.Title = c.Title[..MaxTitleLength];
                c.Problems.Add($"Tiêu đề dài hơn {MaxTitleLength} ký tự → đã cắt bớt.");
            }

            if (string.IsNullOrWhiteSpace(c.Content))
            {
                c.HasError = true;
                c.Problems.Add("Chương không có nội dung.");
            }
            else if (c.Content.Length > MaxContentLength)
            {
                c.HasError = true;
                c.Problems.Add($"Nội dung dài {c.Content.Length:N0} ký tự, vượt giới hạn {MaxContentLength:N0}. Hãy tách thành nhiều chương.");
            }
        }

        // 2c. Trùng số chương NGAY TRONG lần tải này (vd: 2 file cùng là chương 5)
        foreach (var group in chapters.GroupBy(c => c.ChapterNumber!.Value).Where(g => g.Count() > 1))
        {
            var sources = string.Join(" và ", group.Select(c => $"\"{c.Source}\"").Distinct());
            result.Errors.Add($"Chương {group.Key} bị trùng: xuất hiện ở {sources}.");
            foreach (var c in group)
            {
                c.HasError = true;
                c.Problems.Add($"Trùng số chương {group.Key} với chương khác trong lần tải này.");
            }
        }

        // 2d. Chương đã có trong truyện → ghi đè hay bỏ qua
        var preview = new List<ChapterPreviewItemDto>();
        foreach (var c in chapters.OrderBy(c => c.ChapterNumber))
        {
            var no = c.ChapterNumber!.Value;
            string action;
            if (c.HasError)
                action = "Lỗi";
            else if (existingNumbers.Contains(no))
            {
                action = dto.OverwriteExisting ? "Ghi đè" : "Bỏ qua";
                c.Problems.Add(dto.OverwriteExisting
                    ? $"Truyện đã có chương {no} → nội dung cũ sẽ bị THAY bằng nội dung mới."
                    : $"Truyện đã có chương {no} → sẽ BỎ QUA (tick \"Ghi đè\" nếu muốn thay).");
            }
            else
                action = "Thêm mới";

            preview.Add(new ChapterPreviewItemDto
            {
                ChapterNumber = no,
                Title = c.Title,
                ContentLength = c.Content.Length,
                ContentPreview = MakePreview(c.Content),
                Source = c.Source,
                NumberFrom = c.NumberFrom,
                Action = action,
                HasError = c.HasError,
                Problems = c.Problems.ToList()
            });
        }
        result.Chapters = preview;

        // 2e. Cảnh báo THIẾU chương (vd: có 1, 2, 4 mà không có 3)
        var allNumbers = existingNumbers.Concat(preview.Select(p => p.ChapterNumber)).ToHashSet();
        var maxNumber = allNumbers.Max();
        if (maxNumber <= MaxChapterNumber)
        {
            var missing = Enumerable.Range(1, maxNumber).Where(n => !allNumbers.Contains(n)).ToList();
            if (missing.Count > 0)
            {
                var shown = string.Join(", ", missing.Take(20)) + (missing.Count > 20 ? $"... (tổng {missing.Count} chương)" : "");
                result.Warnings.Add($"Truyện đang thiếu chương: {shown}.");
            }
        }

        // ---------- Tổng kết ----------
        var newCount = preview.Count(p => p.Action == "Thêm mới");
        var overwriteCount = preview.Count(p => p.Action == "Ghi đè");
        var skipCount = preview.Count(p => p.Action == "Bỏ qua");
        var errorCount = preview.Count(p => p.Action == "Lỗi");

        // Được lưu khi: không có lỗi chung, không có chương lỗi, và có ít nhất 1 chương để lưu
        result.CanSave = result.Errors.Count == 0 && errorCount == 0 && newCount + overwriteCount > 0;

        result.Message = $"Đọc được {preview.Count} chương: {newCount} thêm mới, {overwriteCount} ghi đè, "
                       + $"{skipCount} bỏ qua, {errorCount} lỗi.";

        if (!result.CanSave && result.Errors.Count == 0 && errorCount == 0)
            result.Errors.Add("Không có chương nào để lưu (tất cả đều đã có trong truyện). Tick \"Ghi đè\" nếu muốn thay nội dung.");

        if (!dto.Save)
            return result;   

        if (!result.CanSave)
        {
            result.Message = "CHƯA LƯU vì còn lỗi. " + result.Message;
            return result;
        }

        var upload = new UploadChaptersDto
        {
            OverwriteExisting = dto.OverwriteExisting,
            Chapters = chapters.Select(c => new CreateChapterDto
            {
                ChapterNumber = c.ChapterNumber!.Value,
                Title = c.Title,
                Content = c.Content,
                AccessLevel = dto.AccessLevel,
                PublicationStatus = dto.PublicationStatus
            }).ToList()
        };

        try
        {
            var saved = await UploadChaptersAsync(storyId, upload);
            result.Saved = true;
            result.Created = saved!.Created;
            result.Updated = saved.Updated;
            result.SkippedNumbers = saved.SkippedNumbers;
            result.Message = saved.Message;
        }
        catch (InvalidOperationException ex)
        {
            result.CanSave = false;
            result.Errors.Add(ex.Message);
            result.Message = "CHƯA LƯU vì có lỗi khi ghi vào database.";
        }

        return result;
    }

    // PHẦN 3: CÁC HÀM PHỤ
  
    // PB10
  
    private static bool IsLocked(string storyAccessPolicy, string chapterAccessLevel)
        => storyAccessPolicy != Free && chapterAccessLevel != Free;


    private static string ResolveAccessLevel(Story story, string requested)
        => story.AccessPolicy == Free ? Free : requested;

    private static string NormalizeContent(string content)
        => content.Replace("\r\n", "\n").Replace('\r', '\n')
                  .Replace('\u2028', '\n').Replace('\u2029', '\n')
                  .Normalize(NormalizationForm.FormC)
                  .Trim();

    // Chuẩn hóa tiêu đề: đổi tiếng Việt về dạng dựng sẵn + bỏ khoảng trắng 2 đầu
    private static string NormalizeTitle(string title)
        => title.Normalize(NormalizationForm.FormC).Trim();

    // Lấy khoảng 100 ký tự đầu, gộp thành 1 dòng — để admin nhìn thử trong bảng xem trước
    private static string MakePreview(string content)
    {
        var oneLine = string.Join(" ", content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return oneLine.Length <= 100 ? oneLine : oneLine[..100] + "…";
    }


    private async Task EnsureNumberFreeAsync(int storyId, int chapterNumber, int? exceptChapterId)
    {
        var taken = await _db.Chapters.AnyAsync(c =>
            c.StoryId == storyId
            && c.ChapterNumber == chapterNumber
            && (exceptChapterId == null || c.ChapterId != exceptChapterId));

        if (taken)
            throw DuplicateNumber(chapterNumber);
    }

    // Lưu database. Phòng trường hợp 2 admin bấm lưu cùng lúc:
    // database vẫn tự chặn trùng → bắt lỗi đó và đổi thành câu thông báo dễ hiểu.
    private async Task SaveOrThrowDuplicateAsync(int? chapterNumber)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            throw chapterNumber is null
                ? new InvalidOperationException("Có số chương vừa bị trùng với dữ liệu khác. Vui lòng tải lại và thử lại.")
                : DuplicateNumber(chapterNumber.Value);
        }
    }


    private static InvalidOperationException DuplicateNumber(int chapterNumber)
        => new($"Truyện đã có chương số {chapterNumber}.");

    private static bool IsDuplicateKey(Exception ex)
        => (ex as MySqlException ?? ex.InnerException as MySqlException)?.Number == 1062;

    private static ChapterListItemDto ToListItem(Chapter c) => new()
    {
        ChapterId = c.ChapterId,
        StoryId = c.StoryId,
        ChapterNumber = c.ChapterNumber,
        Title = c.Title,
        AccessLevel = c.AccessLevel,
        PublicationStatus = c.PublicationStatus,
        PublishedAt = c.PublishedAt,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Chapters;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — CHƯƠNG / ĐỌC
//   PB07–08: tạo / tải lên / sửa chương
//   PB10   : thiết lập & kiểm tra miễn phí / trả phí
//   PB11   : danh sách chương
//   PB12   : đọc miễn phí
//
// MÃ TRẠNG THÁI TRẢ VỀ:
//   200 OK        = thành công, có dữ liệu
//   201 Created   = tạo mới thành công
//   204 NoContent = xóa thành công
//   400 BadRequest= dữ liệu gửi lên sai
//   401 / 403     = chưa đăng nhập / không phải admin
//   404 NotFound  = không tìm thấy
//   409 Conflict  = trùng số chương

[ApiController]
public class ChaptersController : ControllerBase
{
    private const string AdminRole = "Admin";

    // Giới hạn dung lượng 1 lần tải file: 50MB
    private const long MaxUploadBytes = 50 * 1024 * 1024;

    // Service chứa xử lý thật, được ASP.NET tự truyền vào (nhờ dòng đăng ký trong Program.cs)
    private readonly IChapterService _chapterService;

    public ChaptersController(IChapterService chapterService)
    {
        _chapterService = chapterService;
    }

    // Người gọi API có phải admin không? (dựa vào token đăng nhập gửi kèm)
    // Admin được xem trước chương nháp và chương trả phí.
    private bool IsAdmin => User.IsInRole(AdminRole);



    

    //       PHẦN ĐỘC GIẢ — ai cũng gọi được, không cần đăng nhập


    // GET api/stories/5/chapters
    // PB11 — Danh sách chương đã công khai của truyện, kèm ổ khóa (PB10)
    [HttpGet("api/stories/{storyId:int}/chapters")]
    public async Task<ActionResult<List<ChapterListItemDto>>> GetList(int storyId)
    {
        var list = await _chapterService.GetListAsync(storyId, includeDrafts: false);
        return list is null ? NotFound() : Ok(list);
    }

    // GET api/stories/5/chapters/3
    // PB12 — Đọc chương theo SỐ CHƯƠNG (Web dùng)
    [HttpGet("api/stories/{storyId:int}/chapters/{chapterNumber:int}")]
    public async Task<ActionResult<ChapterReadDto>> GetByNumber(int storyId, int chapterNumber)
    {
        var chapter = await _chapterService.GetByNumberAsync(storyId, chapterNumber, IsAdmin);
        return chapter is null ? NotFound() : Ok(chapter);
    }

    // GET api/chapters/12
    // PB12 — Đọc chương theo ID (Mobile dùng)
    [HttpGet("api/chapters/{chapterId:int}")]
    public async Task<ActionResult<ChapterReadDto>> GetById(int chapterId)
    {
        var chapter = await _chapterService.GetByIdAsync(chapterId, IsAdmin);
        return chapter is null ? NotFound() : Ok(chapter);
    }


    //    PHẦN ADMIN — phải đăng nhập tài khoản Admin mới gọi được
    //    [Authorize(Roles = "Admin")] = chặn người không phải admin
   

    // GET api/stories/5/chapters/admin
    // Danh sách TẤT CẢ chương (kể cả nháp) cho trang quản trị
    [Authorize(Roles = AdminRole)]
    [HttpGet("api/stories/{storyId:int}/chapters/admin")]
    public async Task<ActionResult<List<ChapterListItemDto>>> GetListForAdmin(int storyId)
    {
        var list = await _chapterService.GetListAsync(storyId, includeDrafts: true);
        return list is null ? NotFound() : Ok(list);
    }

    // POST api/stories/5/chapters
    // PB07 — Tạo chương mới. PB10 — chọn Free/Paid ngay lúc tạo.
    [Authorize(Roles = AdminRole)]
    [HttpPost("api/stories/{storyId:int}/chapters")]
    public async Task<ActionResult<ChapterListItemDto>> Create(int storyId, CreateChapterDto dto)
    {
        try
        {
            var created = await _chapterService.CreateAsync(storyId, dto);
            if (created is null)
                return NotFound(new { message = "Không tìm thấy truyện." });

            // 201 Created + địa chỉ để xem chương vừa tạo
            return CreatedAtAction(nameof(GetById), new { chapterId = created.ChapterId }, created);
        }
        catch (InvalidOperationException ex)   // trùng số chương
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // POST api/stories/5/chapters/upload
    // PB08 — Tải lên nhiều chương cùng lúc (dạng JSON, client đã tách sẵn)
    [Authorize(Roles = AdminRole)]
    [HttpPost("api/stories/{storyId:int}/chapters/upload")]
    public async Task<ActionResult<UploadChaptersResultDto>> UploadChapters(int storyId, UploadChaptersDto dto)
    {
        try
        {
            var result = await _chapterService.UploadChaptersAsync(storyId, dto);
            return result is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(result);
        }
        catch (InvalidOperationException ex)   // số chương bị lặp trong dữ liệu gửi lên
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/stories/5/chapters/upload-files
    // PB08 — Tải chương lên bằng file .txt / .zip (hoặc văn bản dán)
    // Gửi dạng form có file. Save = false → chỉ kiểm tra; Save = true → kiểm tra rồi lưu.
    // Luôn trả 200 kèm danh sách lỗi/cảnh báo để Web hiện lên màn hình.
    [Authorize(Roles = AdminRole)]
    [HttpPost("api/stories/{storyId:int}/chapters/upload-files")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<ActionResult<UploadChapterFilesResultDto>> UploadChapterFiles(int storyId, [FromForm] UploadChapterFilesDto dto)
    {
        var result = await _chapterService.UploadChapterFilesAsync(storyId, dto);
        return result is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(result);
    }

    // PUT api/chapters/12
    // PB08 — Sửa chương (đổi được tiêu đề, nội dung, số chương, Free/Paid, nháp/công khai)
    [Authorize(Roles = AdminRole)]
    [HttpPut("api/chapters/{chapterId:int}")]
    public async Task<ActionResult<ChapterListItemDto>> Update(int chapterId, UpdateChapterDto dto)
    {
        try
        {
            var updated = await _chapterService.UpdateAsync(chapterId, dto);
            return updated is null ? NotFound(new { message = "Không tìm thấy chương." }) : Ok(updated);
        }
        catch (InvalidOperationException ex)   // trùng số chương
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // DELETE api/chapters/12
    // Xóa chương
    [Authorize(Roles = AdminRole)]
    [HttpDelete("api/chapters/{chapterId:int}")]
    public async Task<IActionResult> Delete(int chapterId)
    {
        var deleted = await _chapterService.DeleteAsync(chapterId);
        return deleted ? NoContent() : NotFound(new { message = "Không tìm thấy chương." });
    }
}
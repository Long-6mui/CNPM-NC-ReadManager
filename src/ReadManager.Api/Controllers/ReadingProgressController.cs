using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Progress;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 2 — PB28 + PB29: TIẾN ĐỘ ĐỌC (Web + Mobile dùng chung)
//   PUT    api/stories/5/progress   → lưu điểm đọc tiếp (chương + vị trí %)
//   GET    api/stories/5/progress   → lấy điểm đọc tiếp của truyện (nút "Đọc tiếp")
//   DELETE api/stories/5/progress   → bỏ truyện khỏi danh sách đang đọc
//   GET    api/me/reading           → PB29: danh sách truyện đang đọc
//
// Tất cả đều phải đăng nhập. Tài khoản bị HẠN CHẾ vẫn dùng được (chỉ đọc, không đăng bài).
[ApiController]
[Authorize]
public class ReadingProgressController : ControllerBase
{
    private readonly IReadingProgressService _progressService;

    public ReadingProgressController(IReadingProgressService progressService)
    {
        _progressService = progressService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // body: { "chapterId": 12, "scrollPercent": 63.5 }
    [HttpPut("api/stories/{storyId:int}/progress")]
    public async Task<ActionResult<ReadingProgressDto>> Save(int storyId, SaveReadingProgressDto dto)
    {
        try
        {
            var saved = await _progressService.SaveAsync(CurrentUserId, storyId, dto);
            return saved is null
                ? NotFound(new { message = "Truyện hoặc chương không tồn tại hoặc chưa công khai." })
                : Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("api/stories/{storyId:int}/progress")]
    public async Task<ActionResult<ReadingProgressDto>> Get(int storyId)
    {
        var progress = await _progressService.GetAsync(CurrentUserId, storyId);
        return progress is null
            ? NotFound(new { message = "Bạn chưa đọc truyện này." })
            : Ok(progress);
    }

    [HttpDelete("api/stories/{storyId:int}/progress")]
    public async Task<IActionResult> Remove(int storyId)
    {
        var removed = await _progressService.RemoveAsync(CurrentUserId, storyId);
        if (!removed)
            return NotFound(new { message = "Truyện này không có trong danh sách đang đọc." });
        return NoContent();
    }

    // GET api/me/reading?page=1&pageSize=20
    [HttpGet("api/me/reading")]
    public async Task<ActionResult<PagedResultDto<ReadingStoryDto>>> MyReading(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _progressService.GetReadingListAsync(CurrentUserId, page, pageSize));
}

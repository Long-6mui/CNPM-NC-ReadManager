using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Statistics.Content;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 3 — PB40: THỐNG KÊ MỨC ĐỘ QUAN TÂM NỘI DUNG (chỉ Admin)
// Mọi API nhận ?from=yyyy-MM-dd&to=yyyy-MM-dd (giờ Việt Nam). Bỏ trống = 30 ngày gần nhất.
//   GET api/admin/statistics/content/overview                    → tổng quan + top 5 truyện / thể loại
//   GET api/admin/statistics/content/stories?sort=reads&top=10   → xếp hạng truyện
//   GET api/admin/statistics/content/stories/5/chapters          → từng chương của 1 truyện
//   GET api/admin/statistics/content/genres                      → theo thể loại
//   GET api/admin/statistics/content/daily                       → từng ngày (vẽ biểu đồ)
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/statistics/content")]
public class ContentStatisticsController : ControllerBase
{
    private readonly IContentStatisticsService _statistics;

    public ContentStatisticsController(IContentStatisticsService statistics)
    {
        _statistics = statistics;
    }

    [HttpGet("overview")]
    public Task<ActionResult<ContentOverviewDto>> Overview([FromQuery] ContentStatsQuery query)
        => Run(() => _statistics.GetOverviewAsync(query));

    [HttpGet("stories")]
    public Task<ActionResult<List<StoryContentStatsDto>>> Stories([FromQuery] ContentStatsQuery query)
        => Run(() => _statistics.GetStoriesAsync(query));

    [HttpGet("stories/{storyId:int}/chapters")]
    public async Task<ActionResult<List<ChapterContentStatsDto>>> Chapters(int storyId, [FromQuery] ContentStatsQuery query)
    {
        try
        {
            var result = await _statistics.GetChaptersAsync(storyId, query);
            return result is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("genres")]
    public Task<ActionResult<List<GenreContentStatsDto>>> Genres([FromQuery] ContentStatsQuery query)
        => Run(() => _statistics.GetGenresAsync(query));

    [HttpGet("daily")]
    public Task<ActionResult<List<DailyContentStatsDto>>> Daily([FromQuery] ContentStatsQuery query)
        => Run(() => _statistics.GetDailyAsync(query));

    // Gọi service, khoảng ngày sai → 400 kèm thông báo
    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

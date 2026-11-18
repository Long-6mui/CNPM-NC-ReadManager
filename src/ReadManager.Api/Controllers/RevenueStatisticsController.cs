using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Statistics.Revenue;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — PB38 + PB39: THỐNG KÊ DOANH THU (chỉ Admin)
// Bộ lọc chung: ?from=yyyy-MM-dd&to=yyyy-MM-dd (giờ Việt Nam, bỏ trống = 30 ngày gần nhất, tối đa 366 ngày)
//               &type=all|plan|story &planVersionId=&storyId=   (không gửi cùng lúc planVersionId và storyId)
//   GET api/admin/statistics/revenue/overview                   → tổng doanh thu, số đơn, gói (mua mới / gia hạn), truyện
//   GET api/admin/statistics/revenue/series?groupBy=day|week|month → doanh thu từng mốc (mốc trống = 0), tuần bắt đầu thứ Hai
//   GET api/admin/statistics/revenue/plans                      → theo từng phiên bản gói đọc (PB38)
//   GET api/admin/statistics/revenue/stories?top=20             → lượt mua và doanh thu theo truyện (PB39)
//   GET api/admin/statistics/revenue/filters                    → gói và truyện từng có đơn đã thanh toán (đổ vào ô chọn)
//   GET api/admin/statistics/revenue/export-csv                 → xuất CSV UTF-8 BOM
// Lỗi: tham số sai → 400 kèm message; gói / truyện không tồn tại → 404; không phải Admin → 403.
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/statistics/revenue")]
public class RevenueStatisticsController : ControllerBase
{
    private readonly IRevenueStatisticsService _revenue;

    public RevenueStatisticsController(IRevenueStatisticsService revenue)
    {
        _revenue = revenue;
    }

    [HttpGet("overview")]
    public Task<ActionResult<RevenueOverviewDto>> Overview([FromQuery] RevenueStatsQuery query)
        => Run(() => _revenue.GetOverviewAsync(query));

    [HttpGet("series")]
    public Task<ActionResult<List<RevenueBucketDto>>> Series([FromQuery] RevenueStatsQuery query)
        => Run(() => _revenue.GetSeriesAsync(query));

    [HttpGet("plans")]
    public Task<ActionResult<List<PlanRevenueDto>>> Plans([FromQuery] RevenueStatsQuery query)
        => Run(() => _revenue.GetPlansAsync(query));

    [HttpGet("stories")]
    public Task<ActionResult<List<StoryRevenueDto>>> Stories([FromQuery] RevenueStatsQuery query)
        => Run(() => _revenue.GetStoriesAsync(query));

    [HttpGet("filters")]
    public async Task<ActionResult<RevenueFilterOptionsDto>> Filters()
        => Ok(await _revenue.GetFilterOptionsAsync());

    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] RevenueStatsQuery query)
    {
        try
        {
            var file = await _revenue.ExportCsvAsync(query);
            return File(file.Content, "text/csv; charset=utf-8", file.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // Gọi service: tham số sai → 400, gói / truyện không có → 404
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Statistics.Activity;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 2 — PB37: THỐNG KÊ HOẠT ĐỘNG THÀNH VIÊN, THUÊ BAO, HÀNH VI ĐỌC & GIAO DỊCH (chỉ Admin)
// Mọi API nhận ?from=yyyy-MM-dd&to=yyyy-MM-dd (giờ Việt Nam). Bỏ trống = 30 ngày gần nhất.
//   GET api/admin/statistics/activity/overview                  → tổng quan KPI, tăng trưởng % so với kỳ trước, top độc giả & live feed
//   GET api/admin/statistics/activity/daily                     → số liệu từng ngày (vẽ biểu đồ đường/cột + DAU)
//   GET api/admin/statistics/activity/transactions              → chi tiết trạng thái giao dịch (Paid, Pending, Failed, Cancelled, Expired)
//   GET api/admin/statistics/activity/plans                     → thống kê đăng ký mới, gia hạn, sắp hết hạn theo từng gói đọc
//   GET api/admin/statistics/activity/members                   → thống kê tài khoản, tỷ lệ trả phí, tỷ lệ giữ chân (Retention/Churn)
//   GET api/admin/statistics/activity/peak-hours                → 24 khung giờ đọc truyện & mua sắm (gợi ý Giờ vàng đăng chương)
//   GET api/admin/statistics/activity/top-readers?sort=score    → bảng xếp hạng Top Độc Giả tích cực & Top VIP ủng hộ
//   GET api/admin/statistics/activity/funnel                    → phễu chuyển đổi 5 bước (Thành viên → Đọc → Tương tác → Tạo đơn → Paid)
//   GET api/admin/statistics/activity/recent-feed?top=20        → dòng sự kiện hoạt động mới nhất theo thời gian thực
//   GET api/admin/statistics/activity/export-csv                → xuất file báo cáo Excel/CSV chuẩn UTF-8 BOM tiếng Việt
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/statistics/activity")]
public class ActivityStatisticsController : ControllerBase
{
    private readonly IActivityStatisticsService _statistics;

    public ActivityStatisticsController(IActivityStatisticsService statistics)
    {
        _statistics = statistics;
    }

    [HttpGet("overview")]
    public Task<ActionResult<ActivityOverviewDto>> Overview([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetOverviewAsync(query));

    [HttpGet("daily")]
    public Task<ActionResult<List<DailyActivityStatsDto>>> Daily([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetDailyAsync(query));

    [HttpGet("transactions")]
    public Task<ActionResult<TransactionStatusSummaryDto>> Transactions([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetTransactionsAsync(query));

    [HttpGet("plans")]
    public Task<ActionResult<List<PlanActivityStatsDto>>> Plans([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetPlansAsync(query));

    [HttpGet("members")]
    public Task<ActionResult<MemberActivitySummaryDto>> Members([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetMembersAsync(query));

    [HttpGet("peak-hours")]
    public Task<ActionResult<List<HourlyActivityStatsDto>>> PeakHours([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetPeakHoursAsync(query));

    [HttpGet("top-readers")]
    public Task<ActionResult<List<TopReaderActivityDto>>> TopReaders([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetTopReadersAsync(query));

    [HttpGet("funnel")]
    public Task<ActionResult<ActivityFunnelDto>> Funnel([FromQuery] ActivityStatsQuery query)
        => Run(() => _statistics.GetFunnelAsync(query));

    [HttpGet("recent-feed")]
    public async Task<ActionResult<List<RecentActivityEventDto>>> RecentFeed([FromQuery] int top = 20)
        => Ok(await _statistics.GetRecentFeedAsync(top));

    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] ActivityStatsQuery query)
    {
        try
        {
            var csvBytes = await _statistics.ExportDailyCsvAsync(query);
            var fileName = $"thong-ke-hoat-dong-{DateTime.UtcNow.AddHours(7):yyyyMMdd-HHmm}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

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

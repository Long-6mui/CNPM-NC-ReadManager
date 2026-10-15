using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — PB33: BÁO CÁO VI PHẠM
//   GET  api/reports/reasons                     → danh sách lý do (ai cũng gọi được)
//   POST api/reports                             → độc giả gửi báo cáo (phải đăng nhập)
//   GET  api/admin/reports?status=Pending&page=1 → Admin xem danh sách
//   PUT  api/admin/reports/5                     → Admin xử lý (Resolved / Rejected)
[ApiController]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("api/reports/reasons")]
    public ActionResult<List<string>> GetReasons() => Ok(_reportService.GetReasons());

    // body: { "targetType": "Comment", "targetId": 7, "reason": "Spam / quảng cáo", "note": "" }
    [Authorize]
    [EnableRateLimiting("comment")]   // dùng chung giới hạn 5 lần / phút với bình luận
    [HttpPost("api/reports")]
    public async Task<ActionResult<ReportDto>> Create(CreateReportDto dto)
    {
        try
        {
            var created = await _reportService.CreateAsync(CurrentUserId, dto);
            return created is null
                ? NotFound(new { message = "Nội dung bị báo cáo không tồn tại hoặc đã bị ẩn." })
                : StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)   // đã báo cáo rồi
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/reports")]
    public async Task<ActionResult<PagedResultDto<ReportDto>>> GetList(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _reportService.GetListAsync(status, page, pageSize));

    // body: { "status": "Resolved", "hideComment": true }
    [Authorize(Roles = "Admin")]
    [HttpPut("api/admin/reports/{reportId:int}")]
    public async Task<ActionResult<ReportDto>> Handle(int reportId, HandleReportDto dto)
    {
        try
        {
            var result = await _reportService.HandleAsync(reportId, dto);
            return result is null ? NotFound(new { message = "Không tìm thấy báo cáo." }) : Ok(result);
        }
        catch (InvalidOperationException ex)   // đã xử lý rồi
        {
            return Conflict(new { message = ex.Message });
        }
    }
}

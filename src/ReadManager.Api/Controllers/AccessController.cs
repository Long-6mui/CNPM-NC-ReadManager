using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Entitlements;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// PB22, PB23 — KIỂM TRA QUYỀN ĐỌC
// Khách vãng lai cũng gọi được (để biết cần đăng nhập hay mua gói).
[ApiController]
public class AccessController : ControllerBase
{
    private readonly IEntitlementService _entitlements;

    public AccessController(IEntitlementService entitlements)
    {
        _entitlements = entitlements;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAdmin => User.IsInRole("Admin");

    // GET api/stories/5/access
    [HttpGet("api/stories/{id:int}/access")]
    public async Task<ActionResult<AccessStatusDto>> CheckStoryAccess(int id)
    {
        var result = await _entitlements.CheckStoryAccessAsync(id, CurrentUserId, IsAdmin);
        if (result.Reason == "NotFound")
            return NotFound(new { message = "Không tìm thấy truyện." });

        return Ok(new AccessStatusDto
        {
            StoryId = id,
            HasAccess = result.CanRead,
            Reason = result.Reason,
            AccessType = result.AccessType
        });
    }

    // GET api/chapters/12/access
    [HttpGet("api/chapters/{id:int}/access")]
    public async Task<ActionResult<AccessStatusDto>> CheckChapterAccess(int id)
    {
        var result = await _entitlements.CheckChapterAccessAsync(id, CurrentUserId, IsAdmin);
        if (result.Reason == "NotFound")
            return NotFound(new { message = "Không tìm thấy chương." });

        return Ok(new AccessStatusDto
        {
            ChapterId = id,
            HasAccess = result.CanRead,
            Reason = result.Reason,
            AccessType = result.AccessType
        });
    }
}

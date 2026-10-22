using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Library;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 2 — THƯ VIỆN & QUYỀN ĐỌC
//   PB27: GET api/me/library             → gói đang dùng + truyện đã mua (đăng nhập)
//         GET api/me/library/purchases   → chỉ danh sách truyện đã mua (đăng nhập)
//   PB22: GET api/stories/5/access       → tôi có đọc được truyện trả phí này không? (khách gọi được)
//         GET api/chapters/12/access     → tôi có đọc được chương này không? (khách gọi được)
//
// Mã trả về: 200 OK | 401 chưa đăng nhập | 404 không thấy truyện/chương (hoặc chưa công khai)
[ApiController]
public class LibraryController : ControllerBase
{
    private const string AdminRole = "Admin";

    private readonly ISubscriptionService _subscriptionService;
    private readonly IEntitlementService _entitlementService;

    public LibraryController(ISubscriptionService subscriptionService, IEntitlementService entitlementService)
    {
        _subscriptionService = subscriptionService;
        _entitlementService = entitlementService;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAdmin => User.IsInRole(AdminRole);

    [Authorize]
    [HttpGet("api/me/library")]
    public async Task<ActionResult<MyLibraryDto>> GetMyLibrary()
        => Ok(await _subscriptionService.GetMyLibraryAsync(CurrentUserId!.Value));

    [Authorize]
    [HttpGet("api/me/library/purchases")]
    public async Task<ActionResult<List<PurchasedStoryDto>>> GetMyPurchases()
        => Ok(await _subscriptionService.GetMyPurchasesAsync(CurrentUserId!.Value));

    [HttpGet("api/stories/{storyId:int}/access")]
    public async Task<ActionResult<AccessDecisionDto>> CheckStory(int storyId)
    {
        var result = await _entitlementService.CheckStoryAsync(CurrentUserId, storyId, IsAdmin);
        return result is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(result);
    }

    [HttpGet("api/chapters/{chapterId:int}/access")]
    public async Task<ActionResult<AccessDecisionDto>> CheckChapter(int chapterId)
    {
        var result = await _entitlementService.CheckChapterAsync(CurrentUserId, chapterId, IsAdmin);
        return result is null ? NotFound(new { message = "Không tìm thấy chương." }) : Ok(result);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Library;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// PB27 — THƯ VIỆN CỦA TÔI
[ApiController]
[Authorize]
[Route("api/me/library")]
public class LibraryController : ControllerBase
{
    private readonly IEntitlementService _entitlements;

    public LibraryController(IEntitlementService entitlements)
    {
        _entitlements = entitlements;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET api/me/library
    [HttpGet]
    public async Task<ActionResult<UserLibraryDto>> GetLibrary()
    {
        var result = await _entitlements.GetMyLibraryAsync(CurrentUserId);
        return Ok(result);
    }

    // GET api/me/library/purchases
    [HttpGet("purchases")]
    public async Task<ActionResult<List<PurchasedStoryItemDto>>> GetPurchases()
    {
        var result = await _entitlements.GetMyLibraryAsync(CurrentUserId);
        return Ok(result.PurchasedStories);
    }
}

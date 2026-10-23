using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Subscriptions;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// PB27 — GÓI ĐĂNG KÝ CỦA TÔI
[ApiController]
[Authorize]
[Route("api/me/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly IEntitlementService _entitlements;

    public SubscriptionsController(IEntitlementService entitlements)
    {
        _entitlements = entitlements;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET api/me/subscriptions
    [HttpGet]
    public async Task<ActionResult<UserSubscriptionSummaryDto>> GetMySubscriptions()
    {
        var result = await _entitlements.GetMySubscriptionsAsync(CurrentUserId);
        return Ok(result);
    }
}

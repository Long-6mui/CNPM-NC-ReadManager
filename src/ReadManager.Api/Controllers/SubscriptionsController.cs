using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Subscriptions;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 2 — PB27: GÓI ĐỌC CỦA TÔI
//   GET api/me/subscriptions   → gói đang dùng (kèm hạn, số ngày còn lại) + lịch sử gói
//
// Mã trả về: 200 OK | 401 chưa đăng nhập
[ApiController]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("api/me/subscriptions")]
    public async Task<ActionResult<MySubscriptionsDto>> GetMine()
        => Ok(await _subscriptionService.GetMySubscriptionsAsync(CurrentUserId));
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.Authentication;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — PB31: THEO DÕI TRUYỆN
//   GET    api/stories/5/follow   → trạng thái theo dõi (ai cũng gọi được)
//   POST   api/stories/5/follow   → theo dõi (phải đăng nhập)
//   DELETE api/stories/5/follow   → bỏ theo dõi
//   GET    api/me/follows         → truyện tôi đang theo dõi
[ApiController]
public class FollowsController : ControllerBase
{
    private readonly IFollowService _followService;

    public FollowsController(IFollowService followService)
    {
        _followService = followService;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("api/stories/{storyId:int}/follow")]
    public async Task<ActionResult<FollowStatusDto>> GetStatus(int storyId)
    {
        var status = await _followService.GetStatusAsync(storyId, CurrentUserId);
        return status is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(status);
    }

    [Authorize(Policy = AuthPolicies.ActiveMember)]
    [HttpPost("api/stories/{storyId:int}/follow")]
    public async Task<ActionResult<FollowStatusDto>> Follow(int storyId)
    {
        var status = await _followService.FollowAsync(storyId, CurrentUserId!.Value);
        return status is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(status);
    }

    [Authorize]
    [HttpDelete("api/stories/{storyId:int}/follow")]
    public async Task<ActionResult<FollowStatusDto>> Unfollow(int storyId)
    {
        var status = await _followService.UnfollowAsync(storyId, CurrentUserId!.Value);
        return status is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(status);
    }

    [Authorize]
    [HttpGet("api/me/follows")]
    public async Task<ActionResult<List<FollowedStoryDto>>> MyFollows()
        => Ok(await _followService.GetMyFollowsAsync(CurrentUserId!.Value));
}

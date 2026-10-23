using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Discovery;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 3 — KHÁM PHÁ, XU HƯỚNG & GỢI Ý
//   GET api/discovery/trending    → từ khóa hot, thể loại hot, truyện nổi bật
//   GET api/discovery/recommended → gợi ý truyện cho người dùng
[ApiController]
[Route("api/discovery")]
public class DiscoveryController : ControllerBase
{
    private readonly IDiscoveryService _discovery;

    public DiscoveryController(IDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("trending")]
    public async Task<ActionResult<TrendingKeywordsDto>> GetTrending()
    {
        var result = await _discovery.GetTrendingAsync();
        return Ok(result);
    }

    [HttpGet("recommended")]
    public async Task<ActionResult<List<StoryListItemDto>>> GetRecommended([FromQuery] int limit = 10)
    {
        var result = await _discovery.GetRecommendedAsync(CurrentUserId, limit);
        return Ok(result);
    }
}

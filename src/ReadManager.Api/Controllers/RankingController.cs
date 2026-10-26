using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Discovery;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 3 — BẢNG XẾP HẠNG TRUYỆN
//   GET api/ranking/stories?by=reads&period=week&top=10
//   GET api/ranking/stories?by=follows&period=month&top=20
[ApiController]
[Route("api/ranking")]
public class RankingController : ControllerBase
{
    private readonly IDiscoveryService _discovery;

    public RankingController(IDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    [HttpGet("stories")]
    public async Task<ActionResult<List<StoryRankingItemDto>>> GetStoryRanking([FromQuery] RankingQueryDto query)
    {
        var result = await _discovery.GetRankingAsync(query);
        return Ok(result);
    }
}

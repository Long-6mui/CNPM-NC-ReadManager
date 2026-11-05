using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

[ApiController, Authorize, Route("api/reader")]
public class ReaderActivityController(AppDbContext db, IEntitlementService rights, IDataProtectionProvider protection) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private readonly IDataProtector tickets = protection.CreateProtector("reader.chapter-session.v1");
    private IQueryable<Story> PublicStories => db.Stories.Where(s => s.Visibility == "Public");

    [AllowAnonymous, HttpGet("ranking")]
    public async Task<IActionResult> Ranking() => Ok(await PublicStories
        .Select(s => new { s.StoryId, s.Title, Hearts = db.ReaderFavorites.Count(f => f.StoryId == s.StoryId) })
        .Where(s => s.Hearts > 0).OrderByDescending(s => s.Hearts).ThenBy(s => s.StoryId).Take(20).ToListAsync());

    [HttpGet("favorites")]
    public async Task<IActionResult> Favorites() => Ok(await db.ReaderFavorites
        .Where(f => f.UserId == UserId && f.Story.Visibility == "Public")
        .Select(f => new { f.StoryId, f.Story.Title, f.Story.AuthorName, f.Story.CoverUrl, f.Story.PublicationStatus, f.Story.AccessPolicy }).ToListAsync());

    [HttpPut("favorites/{id:int}")]
    public async Task<IActionResult> Like(int id)
    {
        if (!await PublicStories.AnyAsync(s => s.StoryId == id)) return NotFound();
        db.ReaderFavorites.Add(new ReaderFavorite { UserId = UserId, StoryId = id });
        await SaveOnceAsync();
        return NoContent();
    }

    [HttpDelete("favorites/{id:int}")]
    public async Task<IActionResult> Unlike(int id)
    {
        await db.ReaderFavorites.Where(f => f.UserId == UserId && f.StoryId == id).ExecuteDeleteAsync();
        return NoContent();
    }

    private async Task<bool> SaveOnceAsync()
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 }) { return false; }
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var exp = 10 * await db.ChapterRewards.CountAsync(r => r.UserId == UserId);
        var prompted = await db.Users.Where(u => u.UserId == UserId).Select(u => u.GenrePromptCompleted).SingleAsync();
        var genres = await db.ReaderGenres.Where(g => g.UserId == UserId).Select(g => g.GenreId).ToListAsync();
        return Ok(new { exp, level = 1 + exp / 100, prompted, genres });
    }

    [HttpPut("genres")]
    public async Task<IActionResult> SetGenres(int[] ids)
    {
        ids = ids.Distinct().ToArray();
        if (await db.Genres.CountAsync(g => ids.Contains(g.GenreId)) != ids.Length)
            return BadRequest(new { message = "ThÃ¡Â»Æ’ loÃ¡ÂºÂ¡i Ã„â€˜ÃƒÂ£ thay Ã„â€˜Ã¡Â»â€¢i. Vui lÃƒÂ²ng tÃ¡ÂºÂ£i lÃ¡ÂºÂ¡i." });
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Users.Where(u => u.UserId == UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.GenrePromptCompleted, true));
        await db.ReaderGenres.Where(g => g.UserId == UserId).ExecuteDeleteAsync();
        db.ReaderGenres.AddRange(ids.Select(id => new ReaderGenre { UserId = UserId, GenreId = id }));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> Recommendations() => Ok(await PublicStories
        .Where(s => db.StoryGenres.Any(sg => sg.StoryId == s.StoryId &&
            db.ReaderGenres.Any(g => g.UserId == UserId && g.GenreId == sg.GenreId)))
        .OrderByDescending(s => s.UpdatedAt).ThenBy(s => s.StoryId)
        .Select(s => new { s.StoryId, s.Title }).Take(8).ToListAsync());

    private async Task<bool> CanRewardAsync(int id) =>
        await db.Chapters.AnyAsync(c => c.ChapterId == id && c.Story.Visibility == "Public" &&
            c.PublicationStatus == "Published" && (c.PublishedAt == null || c.PublishedAt <= DateTime.UtcNow)) &&
        (await rights.CheckChapterAccessAsync(id, UserId, false)).CanRead;

    [HttpPost("chapters/{id:int}/start")]
    public async Task<IActionResult> Start(int id)
    {
        if (!await CanRewardAsync(id)) return Forbid();
        var issued = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Ok(new { ticket = tickets.Protect($"{UserId}:{id}:{issued}") });
    }

    public record ClaimReward(string Ticket, decimal Percent);
    [HttpPost("chapters/{id:int}/finish")]
    public async Task<IActionResult> Finish(int id, ClaimReward request)
    {
        string[] parts;
        try { parts = tickets.Unprotect(request.Ticket).Split(':'); }
        catch (CryptographicException) { return BadRequest(); }
        if (parts.Length != 3 || parts[0] != UserId.ToString() || parts[1] != id.ToString()
            || !long.TryParse(parts[2], out var issued)) return BadRequest();
        var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issued;
        if (elapsed is < 60 or > 86400 || request.Percent is < 99 or > 100) return BadRequest();
        if (!await CanRewardAsync(id)) return Forbid();
        db.ChapterRewards.Add(new ChapterReward { UserId = UserId, ChapterId = id });
        var granted = await SaveOnceAsync();
        return Ok(new { granted });
    }
}

using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// PB31 — THEO DÕI TRUYỆN
public interface IFollowService
{
    // Trả null = truyện không tồn tại / chưa công khai
    Task<FollowStatusDto?> GetStatusAsync(int storyId, int? userId);
    Task<FollowStatusDto?> FollowAsync(int storyId, int userId);
    Task<FollowStatusDto?> UnfollowAsync(int storyId, int userId);
    Task<List<FollowedStoryDto>> GetMyFollowsAsync(int userId);
}

public class FollowService : IFollowService
{
    private const string PublicVisibility = "Public";

    private readonly AppDbContext _db;

    public FollowService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FollowStatusDto?> GetStatusAsync(int storyId, int? userId)
    {
        if (!await StoryIsPublicAsync(storyId))
            return null;
        return await BuildStatusAsync(storyId, userId);
    }

    public async Task<FollowStatusDto?> FollowAsync(int storyId, int userId)
    {
        if (!await StoryIsPublicAsync(storyId))
            return null;

        // Đã theo dõi rồi thì thôi (bấm 2 lần không lỗi)
        var exists = await _db.StoryFollows.AnyAsync(f => f.StoryId == storyId && f.UserId == userId);
        if (!exists)
        {
            _db.StoryFollows.Add(new StoryFollow { StoryId = storyId, UserId = userId, CreatedAt = DateTime.UtcNow });
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 2 lần bấm cùng lúc → database chặn trùng khóa → coi như đã theo dõi
            }
        }
        return await BuildStatusAsync(storyId, userId);
    }

    public async Task<FollowStatusDto?> UnfollowAsync(int storyId, int userId)
    {
        if (!await _db.Stories.AnyAsync(s => s.StoryId == storyId))
            return null;

        await _db.StoryFollows
            .Where(f => f.StoryId == storyId && f.UserId == userId)
            .ExecuteDeleteAsync();
        return await BuildStatusAsync(storyId, userId);
    }

    public async Task<List<FollowedStoryDto>> GetMyFollowsAsync(int userId)
        => await _db.StoryFollows.AsNoTracking()
            .Where(f => f.UserId == userId && f.Story.Visibility == PublicVisibility)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FollowedStoryDto
            {
                StoryId = f.StoryId,
                Title = f.Story.Title,
                CoverUrl = f.Story.CoverUrl,
                FollowedAt = f.CreatedAt
            })
            .ToListAsync();

    // ===== HÀM PHỤ =====
    private Task<bool> StoryIsPublicAsync(int storyId)
        => _db.Stories.AnyAsync(s => s.StoryId == storyId && s.Visibility == PublicVisibility);

    private async Task<FollowStatusDto> BuildStatusAsync(int storyId, int? userId) => new()
    {
        StoryId = storyId,
        FollowerCount = await _db.StoryFollows.CountAsync(f => f.StoryId == storyId),
        IsFollowing = userId.HasValue
            && await _db.StoryFollows.AnyAsync(f => f.StoryId == storyId && f.UserId == userId.Value)
    };
}

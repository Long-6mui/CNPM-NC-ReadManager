using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReadManager.Api.DTOs.Community;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

// BACKEND 4 — PB32: XEM / ĐĂNG / XÓA BÌNH LUẬN
//   GET    api/stories/5/comments?chapterId=12&page=1&pageSize=20   → ai cũng xem được
//   POST   api/stories/5/comments                                   → phải đăng nhập
//   DELETE api/comments/7                                           → người viết hoặc Admin
[ApiController]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentsController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    // Id người đang đăng nhập (null = khách chưa đăng nhập)
    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAdmin => User.IsInRole("Admin");

    // GET api/stories/5/comments
    [HttpGet("api/stories/{storyId:int}/comments")]
    public async Task<ActionResult<PagedResultDto<CommentDto>>> GetList(
        int storyId, [FromQuery] int? chapterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _commentService.GetListAsync(storyId, chapterId, page, pageSize, CurrentUserId, IsAdmin);
        return result is null ? NotFound(new { message = "Không tìm thấy truyện." }) : Ok(result);
    }

    // POST api/stories/5/comments   body: { "chapterId": 12, "content": "Hay quá!" }
    [Authorize]
    [EnableRateLimiting("comment")]   // chống spam: tối đa 5 bình luận / phút (khai báo trong Program.cs)
    [HttpPost("api/stories/{storyId:int}/comments")]
    public async Task<ActionResult<CommentDto>> Create(int storyId, CreateCommentDto dto)
    {
        try
        {
            var created = await _commentService.CreateAsync(storyId, CurrentUserId!.Value, dto);
            return created is null
                ? NotFound(new { message = "Không tìm thấy truyện." })
                : StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)   // nội dung rỗng / chương không hợp lệ
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // DELETE api/comments/7
    [Authorize]
    [HttpDelete("api/comments/{commentId:int}")]
    public async Task<IActionResult> Delete(int commentId)
    {
        var result = await _commentService.DeleteAsync(commentId, CurrentUserId!.Value, IsAdmin);
        return result switch
        {
            DeleteCommentResult.Deleted => NoContent(),
            DeleteCommentResult.Forbidden => Forbid(),
            _ => NotFound(new { message = "Không tìm thấy bình luận." })
        };
    }
}
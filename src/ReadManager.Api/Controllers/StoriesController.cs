using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Services;
using System.Security.Claims;

namespace ReadManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // => api/stories
public class StoriesController : ControllerBase
{
    private readonly IStoryService _storyService;

    public StoriesController(IStoryService storyService)
    {
        _storyService = storyService;
    }

    // GET api/stories?q=&genreId=&status=&access=&sort=updated&page=1&pageSize=12
    // API danh sách truyện phục vụ trang chủ / trang khám phá — chỉ trả truyện Visibility=Public.
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<StoryListItemDto>>> GetList([FromQuery] StoryListQueryDto query)
    {
        var result = await _storyService.GetListAsync(query);
        return Ok(result);
    }

    // GET api/stories/5
    // PB11 — thông tin chi tiết truyện cho ĐỘC GIẢ. Chỉ trả về truyện Visibility=Public;
    // truyện tồn tại nhưng đang Draft/Hidden cũng trả 404 giống hệt "không tồn tại"
    // (không lộ tín hiệu phân biệt cho ai đó dò id tuần tự).
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StoryDetailDto>> GetById(int id)
    {
        var story = await _storyService.GetPublicByIdAsync(id);
        return story is null ? NotFound() : Ok(story);
    }

    // GET api/stories/5/admin
    // Chi tiết truyện cho ADMIN — xem được cả Draft/Hidden để sửa (form Edit cần cái này).
    [Authorize(Roles = "Admin")] // PB03: chỉ Admin
    [HttpGet("{id:int}/admin")]
    public async Task<ActionResult<StoryDetailDto>> GetByIdForAdmin(int id)
    {
        var story = await _storyService.GetByIdForAdminAsync(id);
        return story is null ? NotFound() : Ok(story);
    }

    // POST api/stories
    // PB04 — tạo truyện mới.
    [Authorize(Roles = "Admin")] // PB03: chỉ Admin
    [HttpPost]
    public async Task<ActionResult<StoryDetailDto>> Create(CreateStoryDto dto)
    {
        try
        {
            // Không tin CreatedBy do client gửi: luôn lấy từ người dùng đang đăng nhập.
            dto.CreatedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var created = await _storyService.CreateAsync(dto);
            // Trỏ Location tới route /admin, KHÔNG phải GetById công khai: truyện mới tạo
            // mặc định Visibility=Draft, nếu trỏ về GetById (đã lọc Public) sẽ 404 ngay
            // sau khi vừa tạo thành công — rất khó hiểu cho người gọi API.
            return CreatedAtAction(nameof(GetByIdForAdmin), new { id = created.StoryId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT api/stories/5
    // PB05 — sửa truyện + đổi trạng thái phát hành. PB06 — gán lại thể loại (qua GenreIds).
    [Authorize(Roles = "Admin")] // PB03: chỉ Admin
    [HttpPut("{id:int}")]
    public async Task<ActionResult<StoryDetailDto>> Update(int id, UpdateStoryDto dto)
    {
        try
        {
            var updated = await _storyService.UpdateAsync(id, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
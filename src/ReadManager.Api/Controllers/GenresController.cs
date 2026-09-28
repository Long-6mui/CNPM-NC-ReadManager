using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.Genres;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // => api/genres
public class GenresController : ControllerBase
{
    private readonly IGenreService _genreService;

    public GenresController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    // GET api/genres
    // Dùng để: đổ dropdown lọc theo thể loại ở trang chủ, và đổ checkbox chọn thể loại ở form tạo/sửa truyện.
    [HttpGet]
    public async Task<ActionResult<List<GenreDto>>> GetAll()
    {
        var genres = await _genreService.GetAllAsync();
        return Ok(genres);
    }

    // POST api/genres
    // TODO(auth): gắn [Authorize(Roles = "Admin")] khi PB02/PB03 xong.
    [HttpPost]
    public async Task<ActionResult<GenreDto>> Create(CreateGenreDto dto)
    {
        try
        {
            var created = await _genreService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll), created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // DELETE api/genres/5
    // TODO(auth): gắn [Authorize(Roles = "Admin")] khi PB02/PB03 xong.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _genreService.DeleteAsync(id);

        return result switch
        {
            DeleteGenreResult.NotFound => NotFound(),
            DeleteGenreResult.InUse => Conflict(new
            {
                message = "Thể loại đang được gán cho ít nhất một truyện, không thể xóa."
            }),
            _ => NoContent()
        };
    }
}
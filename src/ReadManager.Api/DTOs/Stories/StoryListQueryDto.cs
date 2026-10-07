namespace ReadManager.Api.DTOs.Stories;

// Tham số cho GET /api/stories — bind từ query string, ví dụ:
// /api/stories?q=kiem&genreId=3&sort=newest&page=2&pageSize=12
public class StoryListQueryDto
{
    public string? Q { get; set; }
    public int? GenreId { get; set; }
    public string? Status { get; set; }        // Ongoing | Completed | Paused — bỏ trống = tất cả
    public string? Access { get; set; }        // Free | Mixed | Paid — bỏ trống = tất cả
    public string Sort { get; set; } = "updated"; // updated | newest | title

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
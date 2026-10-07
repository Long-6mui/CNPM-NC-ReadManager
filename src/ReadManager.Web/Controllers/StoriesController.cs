using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Controllers;

public class StoriesController(StoriesApiClient api) : Controller
{
    public async Task<IActionResult> Browse(string? q, int? genre, AccessPolicy? access,
        StoryStatus? status, string sort = "updated")
    {
        var vm = new BrowseVm { Q = q?.Trim(), GenreId = genre, Access = access, Status = status, Sort = sort };
        try
        {
            vm.Genres = (await api.GenresAsync()).Select(g => new GenreVm(g.GenreId, g.Name)).ToList();
            var result = await api.ListAsync(vm.Q, genre, status?.ToString(), access?.ToString(), sort, 1, 48);
            vm.Stories = result.Items.Select(StoriesApiClient.ToCard).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = "Không kết nối được API. Hãy chạy ReadManager.Api rồi tải lại trang.";
        }
        return View(vm);
    }

    private const int ChaptersPerPage = 20;   // 20 chương/trang = 10 hàng x 2 cột

    // Trang chi tiết truyện + danh sách chương (PB11), có phân trang
    public async Task<IActionResult> Details(int id, int page = 1)
    {
        try
        {
            var detail = await api.GetStoryAsync(id);
            if (detail is null) return NotFound();
            var chapters = await api.ChaptersAsync(id);

            // Phân trang: giữ số trang trong khoảng hợp lệ
            var pageCount = Math.Max(1, (int)Math.Ceiling(chapters.Count / (double)ChaptersPerPage));
            page = Math.Clamp(page, 1, pageCount);

            return View(new StoryDetailVm
            {
                Story = StoriesApiClient.ToStory(detail),
                GenreNames = detail.Genres.Select(g => g.Name).ToList(),
                Chapters = chapters
                    .Skip((page - 1) * ChaptersPerPage)   // bỏ qua các chương của trang trước
                    .Take(ChaptersPerPage)                // lấy 20 chương của trang này
                    .Select(c => new ChapterRowVm
                    {
                        No = c.ChapterNumber,
                        Title = c.Title,
                        Locked = c.IsLocked,               // PB10 — hiện 🔒
                        CreatedAt = c.PublishedAt ?? c.CreatedAt
                    }).ToList(),
                TotalChapters = chapters.Count,
                ChapterPage = page,
                ChapterPageCount = pageCount,
                FirstVisibleChapterNo = chapters.FirstOrDefault()?.ChapterNumber   // nút "Đọc từ đầu"
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
            return RedirectToAction("Index", "Home");
        }
    }

    // Trang đọc chương (PB12)
    public async Task<IActionResult> Read(int storyId, int no)
    {
        try
        {
            // Admin đăng nhập thì gửi kèm token để xem trước chương nháp / trả phí.
            var token = await HttpContext.GetTokenAsync("access_token");
            var c = await api.GetChapterAsync(storyId, no, token);
            if (c is null)
            {
                TempData["Toast"] = "Không tìm thấy chương này.";
                return RedirectToAction(nameof(Details), new { id = storyId });
            }

            return View(new ReadVm
            {
                Story = new Story { Id = c.StoryId, Title = c.StoryTitle },
                Chapter = new Chapter
                {
                    Id = c.ChapterId,
                    StoryId = c.StoryId,
                    No = c.ChapterNumber,
                    Title = c.Title,
                    Content = c.Content,
                    IsFree = c.AccessLevel == "Free"
                },
                Locked = c.IsLocked,                                  // true → hiện hộp "cần mở khóa"
                NotYetPublicPreview = c.PublicationStatus != "Published",
                PrevNo = c.PreviousChapterNumber,                     // nút "Chương trước"
                NextNo = c.NextChapterNumber                          // nút "Chương sau"
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
            return RedirectToAction(nameof(Details), new { id = storyId });
        }
    }
}
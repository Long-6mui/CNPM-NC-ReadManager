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

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var detail = await api.GetStoryAsync(id);
            if (detail is null) return NotFound();
            return View(new StoryDetailVm
            {
                Story = StoriesApiClient.ToStory(detail),
                GenreNames = detail.Genres.Select(g => g.Name).ToList(),
                Chapters = [],               // sẽ nối khi Backend 4 xong API chương
                FirstVisibleChapterNo = null
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
            return RedirectToAction("Index", "Home");
        }
    }

    // Tạm thời: chưa có API chương nên không đọc được
    public IActionResult Read(int storyId, int no)
    {
        TempData["Toast"] = "Đọc chương sẽ hoạt động khi API chương (Backend 4) hoàn thành.";
        return RedirectToAction(nameof(Details), new { id = storyId });
    }
}
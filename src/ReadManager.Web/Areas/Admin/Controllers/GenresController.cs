using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class GenresController(IWebHostEnvironment environment, StoriesApiClient api) : PreviewController(environment)
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";

    public async Task<IActionResult> Index()
    {
        try
        {
            var genres = await api.GenresAsync();
            ViewBag.Counts = genres.ToDictionary(g => g.GenreId, g => g.StoryCount);
            return View(genres.Select(g => new GenreVm(g.GenreId, g.Name)).ToList());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return View(new List<GenreVm>());
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Toast"] = "Vui lòng nhập tên thể loại.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var result = await api.CreateGenreAsync(name.Trim(), await HttpContext.GetTokenAsync("access_token"));
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var result = await api.DeleteGenreAsync(id, await HttpContext.GetTokenAsync("access_token"));
            TempData["Toast"] = result.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index));
    }
}
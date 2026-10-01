using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Models;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Controllers;

public class HomeController(StoriesApiClient api) : Controller
{
    public async Task<IActionResult> Index(string? q, int? genre)
    {
        var query = q?.Trim();
        try
        {
            var genres = await api.GenresAsync();
            var updated = await api.ListAsync(query, genre, null, null, "updated");
            var newest = await api.ListAsync(query, genre, null, null, "newest");
            var free = await api.ListAsync(query, genre, null, "Free", "updated");

            return View(new HomeVm
            {
                Updated = updated.Items.Select(StoriesApiClient.ToCard).ToList(),
                Newest = newest.Items.Select(StoriesApiClient.ToCard).ToList(),
                Free = free.Items.Select(StoriesApiClient.ToCard).ToList(),
                Genres = genres.Select(g => new GenreVm(g.GenreId, g.Name)).ToList(),
                TotalStories = updated.TotalCount,
                TotalGenres = genres.Count,
                TotalChapters = updated.Items.Sum(s => s.PublishedChapterCount),
                Query = query,
                GenreId = genre
            });
        }
        catch (HttpRequestException)
        {
            TempData["Toast"] = "Không k?t n?i ???c API. Hãy ch?y ReadManager.Api r?i t?i l?i trang.";
            return View(new HomeVm { Query = query, GenreId = genre });
        }
        catch (TaskCanceledException)
        {
            TempData["Toast"] = "API ph?n h?i quá ch?m. Vui lòng th? l?i.";
            return View(new HomeVm { Query = query, GenreId = genre });
        }
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
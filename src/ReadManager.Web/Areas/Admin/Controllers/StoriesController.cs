using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class StoriesController(IWebHostEnvironment environment, StoriesApiClient api) : PreviewController(environment)
{
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");
    private async Task<List<GenreVm>> LoadGenres()
        => (await api.GenresAsync()).Select(g => new GenreVm(g.GenreId, g.Name)).ToList();

    public async Task<IActionResult> Index()
    {
        try
        {
            var result = await api.ListAsync(null, null, null, null, "updated", 1, 50);
            ViewBag.ChapterCounts = result.Items.ToDictionary(s => s.StoryId, s => s.PublishedChapterCount);
            return View(result.Items.Select(StoriesApiClient.ToListStory).ToList());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return View(new List<Story>());
        }
    }

    public async Task<IActionResult> Create()
    {
        try { return View("Edit", new StoryFormVm { AllGenres = await LoadGenres(), Visibility = "Public" }); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var d = await api.GetStoryForAdminAsync(id, await Token());
            if (d is null) return NotFound();
            return View(new StoryFormVm
            {
                Id = d.StoryId,
                Title = d.Title,
                Author = d.AuthorName,
                Description = d.Synopsis,
                Status = Enum.TryParse<StoryStatus>(d.PublicationStatus, out var st) ? st : StoryStatus.Ongoing,
                Access = Enum.TryParse<AccessPolicy>(d.AccessPolicy, out var ac) ? ac : AccessPolicy.Free,
                Visibility = d.Visibility,
                Price = d.CurrentPrice ?? 0,
                CoverUrl = d.CoverUrl,
                FreeChapterCount = d.FreeChapterCount,
                SelectedGenreIds = d.Genres.Select(g => g.GenreId).ToList(),
                AllGenres = await LoadGenres()
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(StoryFormVm vm)
    {
        // Tác giả và giới thiệu được phép để trống
        ModelState.Remove(nameof(vm.Author));
        ModelState.Remove(nameof(vm.Description));
        try
        {
            if (!ModelState.IsValid)
            {
                vm.AllGenres = await LoadGenres();
                return View("Edit", vm);
            }

            decimal? price = vm.Access == AccessPolicy.Free || vm.Price <= 0 ? null : vm.Price;
            var cover = string.IsNullOrWhiteSpace(vm.CoverUrl) ? null : vm.CoverUrl.Trim();
            object body;
            if (vm.Id == 0)
            {
                body = new
                {
                    title = vm.Title.Trim(),
                    authorName = vm.Author ?? "",
                    synopsis = vm.Description ?? "",
                    coverUrl = cover,
                    accessPolicy = vm.Access.ToString(),
                    visibility = vm.Visibility,
                    currentPrice = price,
                    genreIds = vm.SelectedGenreIds,
                    createdBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                };
            }
            else
            {
                body = new
                {
                    title = vm.Title.Trim(),
                    authorName = vm.Author ?? "",
                    synopsis = vm.Description ?? "",
                    coverUrl = cover,
                    publicationStatus = vm.Status.ToString(),
                    accessPolicy = vm.Access.ToString(),
                    visibility = vm.Visibility,
                    currentPrice = price,
                    genreIds = vm.SelectedGenreIds
                };
            }

            var result = await api.SaveStoryAsync(vm.Id, body, await Token());
            if (result.Ok)
            {
                TempData["Toast"] = result.Message;
                return RedirectToAction(nameof(Edit), new { id = result.Id });
            }
            ModelState.AddModelError("", result.Message);
            vm.AllGenres = await LoadGenres();
            return View("Edit", vm);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError("", ApiDown);
            return View("Edit", vm);
        }
    }

    // API chưa có chức năng xóa truyện
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        TempData["Toast"] = "API chưa có chức năng xóa truyện (cần Backend 3 bổ sung).";
        return RedirectToAction(nameof(Index));
    }

    // ----- Phần chương: chờ Backend 4 -----
    private IActionResult ChaptersPending(int storyId)
    {
        TempData["Toast"] = "Quản lý chương sẽ hoạt động khi Backend 4 hoàn thành API chương.";
        return RedirectToAction(nameof(Edit), new { id = storyId });
    }
    public IActionResult ChapterEdit(int storyId, int? no) => ChaptersPending(storyId);
    [HttpPost, ValidateAntiForgeryToken] public IActionResult ChapterSave(ChapterFormVm vm) => ChaptersPending(vm.StoryId);
    [HttpPost, ValidateAntiForgeryToken] public IActionResult ChapterDelete(int storyId, int no) => ChaptersPending(storyId);

    public async Task<IActionResult> BulkImport(int storyId)
    {
        try
        {
            var d = await api.GetStoryForAdminAsync(storyId, await Token());
            return d is null ? NotFound() : View(new BulkImportVm { StoryId = storyId, StoryTitle = d.Title });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult BulkImportSave(BulkImportVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.RawText) || vm.RawText.Length > 200000)
            ModelState.AddModelError("", "Nhập nội dung từ 1 đến 200.000 ký tự.");
        else
        {
            var result = ChapterBulkParser.Parse(vm.RawText);
            ModelState.AddModelError("", $"Nhận diện {result.Count} chương. Đây là kiểm tra nội dung; chưa lưu DB (chờ API chương).");
        }
        return View("BulkImport", vm);
    }
}
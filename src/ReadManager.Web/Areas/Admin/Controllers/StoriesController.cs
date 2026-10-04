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
                AllGenres = await LoadGenres(),
                // BE4 — lấy danh sách chương (kể cả nháp) hiện ở khung "Chương" bên phải
                Chapters = (await api.ChaptersForAdminAsync(id, await Token()))
                    .Select(StoriesApiClient.ToChapter).ToList()
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

    // ----- PHẦN CHƯƠNG (Backend 4) -----

    // Mở form thêm chương (no = null) hoặc sửa chương (no = số chương)
    public async Task<IActionResult> ChapterEdit(int storyId, int? no)
    {
        try
        {
            var token = await Token();
            var story = await api.GetStoryForAdminAsync(storyId, token);
            if (story is null) return NotFound();

            if (no is null)
            {
                // Chương mới: tự gợi ý số chương tiếp theo (số lớn nhất + 1)
                var list = await api.ChaptersForAdminAsync(storyId, token);
                return View(new ChapterFormVm
                {
                    StoryId = storyId,
                    StoryTitle = story.Title,
                    No = list.Count == 0 ? 1 : list.Max(c => c.ChapterNumber) + 1,
                    IsFree = story.AccessPolicy == "Free",
                    Status = ChapterStatus.Reviewed
                });
            }

            // Sửa chương: lấy nội dung chương cũ đổ vào form
            var c = await api.GetChapterAsync(storyId, no.Value, token);
            if (c is null) return NotFound();
            return View(new ChapterFormVm
            {
                Id = c.ChapterId,
                StoryId = storyId,
                StoryTitle = story.Title,
                No = c.ChapterNumber,
                Title = c.Title,
                Content = c.Content,
                IsFree = c.AccessLevel == "Free",
                Status = c.PublicationStatus == "Published" ? ChapterStatus.Reviewed : ChapterStatus.Draft
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
            return RedirectToAction(nameof(Edit), new { id = storyId });
        }
    }

    // Bấm "Lưu chương": Id = 0 → tạo mới, Id > 0 → sửa
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChapterSave(ChapterFormVm vm)
    {
        if (!ModelState.IsValid) return View("ChapterEdit", vm);
        try
        {
            var body = new
            {
                chapterNumber = vm.No,
                title = vm.Title.Trim(),
                content = vm.Content,
                accessLevel = vm.IsFree ? "Free" : "Paid",                                         // PB10
                publicationStatus = vm.Status == ChapterStatus.Reviewed ? "Published" : "Draft"
            };
            var result = await api.SaveChapterAsync(vm.StoryId, vm.Id, body, await Token());
            if (result.Ok)
            {
                TempData["Toast"] = result.Message;
                return RedirectToAction(nameof(Edit), new { id = vm.StoryId });
            }
            ModelState.AddModelError("", result.Message);   // vd: "Truyện đã có chương số 3."
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError("", ApiDown);
        }
        return View("ChapterEdit", vm);
    }

    // Xóa chương: tìm Id của chương theo số chương rồi gọi API xóa
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChapterDelete(int storyId, int no)
    {
        try
        {
            var token = await Token();
            var c = await api.GetChapterAsync(storyId, no, token);
            TempData["Toast"] = c is null
                ? "Không tìm thấy chương."
                : (await api.DeleteChapterAsync(c.ChapterId, token)).Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Edit), new { id = storyId });
    }

    // Mở trang "Tải nhiều chương"
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

    // ----- Tải nhiều chương: file .txt / .zip / văn bản dán -----
    // Trang BulkImport gọi action này bằng JavaScript (fetch), KHÔNG tải lại trang,
    // nhờ vậy file đã chọn vẫn còn khi bấm "Kiểm tra" rồi bấm "Lưu".
    // save = false → chỉ kiểm tra, trả bảng xem trước + lỗi | save = true → lưu nếu không có lỗi.
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<IActionResult> BulkImportCheck(int storyId, List<IFormFile> files, string? text,
        bool markFree, bool publish, bool overwriteExisting, bool save)
    {
        try
        {
            // Gói lại thành form để chuyển tiếp sang API
            using var form = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var part = new StreamContent(file.OpenReadStream());
                part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                form.Add(part, "Files", file.FileName);
            }
            if (!string.IsNullOrWhiteSpace(text)) form.Add(new StringContent(text), "Text");
            form.Add(new StringContent(markFree ? "Free" : "Paid"), "AccessLevel");
            form.Add(new StringContent(publish ? "Published" : "Draft"), "PublicationStatus");
            form.Add(new StringContent(overwriteExisting ? "true" : "false"), "OverwriteExisting");
            form.Add(new StringContent(save ? "true" : "false"), "Save");

            var (status, body) = await api.UploadChapterFilesAsync(storyId, form, await Token());

            if (status == System.Net.HttpStatusCode.OK)
            {
                // Lưu thành công → để sẵn thông báo cho trang sửa truyện (JS sẽ chuyển trang)
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("saved", out var saved) && saved.GetBoolean()
                    && doc.RootElement.TryGetProperty("message", out var msg))
                    TempData["Toast"] = msg.GetString();
                return Content(body, "application/json");
            }

            return Json(ErrorJson(status switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Phiên đăng nhập hết hạn. Hãy đăng nhập lại.",
                System.Net.HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
                System.Net.HttpStatusCode.NotFound => "Không tìm thấy truyện.",
                System.Net.HttpStatusCode.RequestEntityTooLarge => "File quá lớn (tối đa 50MB mỗi lần).",
                _ => $"API trả về lỗi {(int)status}."
            }));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Json(ErrorJson(ApiDown));
        }
    }

    // Định dạng lỗi giống kết quả của API để JavaScript hiện chung một kiểu
    private static object ErrorJson(string message) => new
    {
        canSave = false,
        saved = false,
        errors = new[] { message },
        warnings = Array.Empty<string>(),
        chapters = Array.Empty<object>(),
        message = "Có lỗi xảy ra."
    };
}
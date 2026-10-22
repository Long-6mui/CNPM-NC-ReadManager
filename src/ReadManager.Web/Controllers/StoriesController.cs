using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Controllers;

public class StoriesController(StoriesApiClient api, DemoReadingProgressStore progress, DemoMemberStore members) : Controller
{
    public async Task<IActionResult> Browse(string? q, int? genre, AccessPolicy? access,
        StoryStatus? status, string sort = "updated", int page = 1)
    {
        const int pageSize = 24;
        page = Math.Max(1, page);
        var vm = new BrowseVm { Q = q?.Trim(), GenreId = genre, Access = access, Status = status, Sort = sort };
        try
        {
            vm.Genres = (await api.GenresAsync()).Select(g => new GenreVm(g.GenreId, g.Name)).ToList();
            var result = await api.ListAsync(vm.Q, genre, status?.ToString(), access?.ToString(), sort, page, pageSize);
            vm.Total = result.TotalCount;
            vm.PageCount = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)pageSize));
            if (page > vm.PageCount)   // trang vượt quá → lấy trang cuối
            {
                page = vm.PageCount;
                result = await api.ListAsync(vm.Q, genre, status?.ToString(), access?.ToString(), sort, page, pageSize);
            }
            vm.Page = page;
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

            // PB31 — trạng thái theo dõi (gửi token để biết mình đã theo dõi chưa)
            var token = await HttpContext.GetTokenAsync("access_token");
            var follow = await api.FollowStatusAsync(id, token);
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

            // PB21 — báo giá mua truyện: đã sở hữu chưa / có đơn đang chờ không
            var quote = await api.QuoteStoryAsync(id, token);

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
                        UpcomingAt = c.IsUpcoming ? StoriesApiClient.ToLocal(c.ScheduledAt) : null,   // hẹn giờ ra mắt
                        CreatedAt = c.PublishedAt ?? c.CreatedAt
                    }).ToList(),
                TotalChapters = chapters.Count,
                ChapterPage = page,
                ChapterPageCount = pageCount,
                // nút "Đọc từ đầu" → chương đầu tiên đã ra mắt (bỏ qua chương còn hẹn giờ)
                FirstVisibleChapterNo = chapters.FirstOrDefault(c => !c.IsUpcoming)?.ChapterNumber,
                IsFollowing = follow?.IsFollowing ?? false,
                Owned = quote?.AlreadyOwned ?? false,
                PendingOrderId = quote?.PendingOrderId,
                HasPendingOrder = quote?.PendingOrderId != null,
                CanBuy = quote?.CanBuy ?? false,
                BuyPrice = quote?.Amount ?? 0,
                // PB28 — nút "Đọc tiếp" trỏ đến chương đọc gần nhất
                ContinueChapterNo = uid != "" ? progress.Get(uid, id)?.ChapterNo : null,
                FollowerCount = follow?.FollowerCount ?? 0
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

            var vm = new ReadVm
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
                UpcomingAt = c.IsUpcoming ? StoriesApiClient.ToLocal(c.ScheduledAt) : null,   // chưa tới giờ ra mắt
                NotYetPublicPreview = c.PublicationStatus != "Published",
                PrevNo = c.PreviousChapterNumber,                     // nút "Chương trước"
                NextNo = c.NextChapterNumber                          // nút "Chương sau"
            };

            // PB28 — nhớ chương đọc gần nhất (chỉ khi người đọc đã đăng nhập và thật sự xem được nội dung)
            var readerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            if (readerId != "" && !c.IsLocked && !c.IsUpcoming && c.PublicationStatus == "Published")
                progress.Save(readerId, storyId, c.StoryTitle, c.ChapterNumber, c.Title);

            // PB36 — tài khoản đang bị hạn chế thì ẩn ô bình luận và báo lý do
            if (readerId != "" && members.StateOf(readerId) is (MemberState.Restricted, { } restr))
                vm.RestrictionNote = $"Tài khoản của bạn đang bị hạn chế bình luận {(restr.Until is { } u ? "đến " + u.ToLocalTime().ToString("dd/MM/yyyy") : "cho đến khi quản trị viên khôi phục")}. Lý do: {restr.Reason}";

            // PB32 + PB33 — chương đã ra mắt thì tải bình luận + danh sách lý do báo cáo
            if (!c.IsUpcoming)
            {
                var comments = await api.CommentsAsync(storyId, c.ChapterId, token);
                vm.Comments = comments.Items;
                vm.CommentTotal = comments.TotalCount;
                vm.ReportReasons = await api.ReportReasonsAsync();
            }

            return View(vm);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";
            return RedirectToAction(nameof(Details), new { id = storyId });
        }
    }

    // =====================================================================
    // BACKEND 4 — TƯƠNG TÁC: các nút bấm trên trang (gọi API rồi quay lại trang cũ)
    // =====================================================================
    private const string ApiDown = "Không kết nối được API. Hãy chạy ReadManager.Api rồi thử lại.";

    // Quay về trang đọc chương, nhảy xuống mục bình luận
    private IActionResult BackToRead(int storyId, int no)
        => Redirect(Url.Action(nameof(Read), new { storyId, no }) + "#comments");

    // ----- PB32: đăng bình luận -----
    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PostComment(int storyId, int no, int chapterId, string? content)
    {
        // PB36 — chặn bình luận khi tài khoản đang bị hạn chế (kiểm tra cả phía server, không chỉ ẩn ô nhập)
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        if (members.StateOf(uid).State != MemberState.Active)
        {
            TempData["Toast"] = "Tài khoản của bạn đang bị hạn chế, không thể bình luận.";
            return BackToRead(storyId, no);
        }
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Toast"] = "Vui lòng nhập nội dung bình luận.";
            return BackToRead(storyId, no);
        }
        try
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            TempData["Toast"] = (await api.PostCommentAsync(storyId, chapterId, content, token)).Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return BackToRead(storyId, no);
    }

    // ----- PB32: xóa bình luận (người viết hoặc Admin) -----
    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int commentId, int storyId, int no)
    {
        try
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            TempData["Toast"] = (await api.DeleteCommentAsync(commentId, token)).Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return BackToRead(storyId, no);
    }

    // ----- PB33: báo cáo vi phạm (chương hoặc bình luận) -----
    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(string targetType, int targetId, string? reason, string? note, int storyId, int no)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Toast"] = "Vui lòng chọn lý do báo cáo.";
            return BackToRead(storyId, no);
        }
        try
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            TempData["Toast"] = (await api.CreateReportAsync(targetType, targetId, reason, note, token)).Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return BackToRead(storyId, no);
    }

    // ----- PB31: theo dõi / bỏ theo dõi truyện -----
    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(int id, bool follow)
    {
        try
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            TempData["Toast"] = (await api.SetFollowAsync(id, follow, token)).Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["Toast"] = ApiDown;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}

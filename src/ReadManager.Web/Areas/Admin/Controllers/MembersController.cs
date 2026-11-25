using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Areas.Admin.Controllers;

public class MembersIndexVm
{
    public List<ReadingProgressApiClient.DirectoryMember> Items { get; set; } = [];
    public string? Q { get; set; }
    public string? State { get; set; }
    public string? Role { get; set; }
    public int Page { get; set; } = 1;
    public int PageCount { get; set; } = 1;
    public int Total { get; set; }
}

// PB35 + PB36 — quản lý thành viên: xem danh sách, thông tin sử dụng, hạn chế / tạm ngừng / khôi phục.
// Toàn bộ dữ liệu và thao tác đổi trạng thái đi qua API (api/admin/users...) và được lưu trong DB.
[Area("Admin")]
public class MembersController(IWebHostEnvironment environment) : PreviewController(environment)
{
    private const int PageSize = 10;
    private const string ApiDown = "Không kết nối được API. Hãy kiểm tra kết nối và phiên đăng nhập admin.";
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");

    public async Task<IActionResult> Index([FromServices] ReadingProgressApiClient reader, string? q, string? state, string? role, int page = 1)
    {
        var vm = new MembersIndexVm { Q = q, State = state, Role = role };
        try
        {
            var rows = await reader.FindMembersAsync(q, role, await Token(), state);
            vm.Total = rows.Count;
            vm.PageCount = Math.Max(1, (vm.Total + PageSize - 1) / PageSize);
            vm.Page = Math.Clamp(page, 1, vm.PageCount);
            vm.Items = rows.Skip((vm.Page - 1) * PageSize).Take(PageSize).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ViewBag.DirectoryError = "Không tải được danh sách thành viên từ API. Hãy kiểm tra kết nối và phiên đăng nhập admin.";
        }
        return View(vm);
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Grant(Guid requestId, string email, string kind, int amount,
        [FromServices] ReadingProgressApiClient reader)
    {
        if (!ModelState.IsValid || amount < 1 || kind is not ("coins" or "days"))
            TempData["GrantMessage"] = "Nhập email và số nguyên dương.";
        else
        {
            try
            {
                TempData["GrantMessage"] = await reader.GrantAsync(requestId, email, kind == "coins" ? amount : 0,
                    kind == "days" ? amount : 0, await Token());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            { TempData["GrantMessage"] = "Chưa xác nhận được kết quả. Hãy kiểm tra tài khoản trước khi thử lại."; }
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Detail(int id, [FromServices] ReadingProgressApiClient reader)
    {
        try
        {
            var member = await reader.GetMemberAsync(id, await Token());
            if (member is not null) return View(member);
            TempData["Toast"] = "Không tìm thấy thành viên.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { TempData["Toast"] = ApiDown; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Restrict(int id, string kind, string? reason, [FromServices] ReadingProgressApiClient reader)
    {
        var status = kind == "Suspended" ? "Suspended" : "Restricted";
        return ChangeStatus(reader, id, status, reason,
            status == "Suspended" ? "Đã tạm ngừng tài khoản." : "Đã hạn chế bình luận của tài khoản.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Restore(int id, [FromServices] ReadingProgressApiClient reader)
        => ChangeStatus(reader, id, "Active", null, "Đã khôi phục tài khoản.");

    private async Task<IActionResult> ChangeStatus(ReadingProgressApiClient reader, int id, string status, string? reason, string okMessage)
    {
        try { TempData["Toast"] = await reader.ChangeStatusAsync(id, status, reason, await Token()) ?? okMessage; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { TempData["Toast"] = "Chưa xác nhận được kết quả. Hãy tải lại trang để kiểm tra trạng thái tài khoản."; }
        return RedirectToAction(nameof(Detail), new { id });
    }
}

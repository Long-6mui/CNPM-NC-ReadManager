using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;

namespace ReadManager.Web.Areas.Admin.Controllers;

public class MembersIndexVm
{
    public List<MemberItem> Items { get; set; } = [];
    public Dictionary<string, (MemberState State, MemberRestriction? Info)> States { get; set; } = [];
    public string? Q { get; set; }
    public string? State { get; set; }
    public string? Role { get; set; }
    public int Page { get; set; } = 1;
    public int PageCount { get; set; } = 1;
    public int Total { get; set; }
}

public class MemberDetailVm
{
    public MemberItem Member { get; set; } = null!;
    public MemberState State { get; set; }
    public MemberRestriction? Restriction { get; set; }
    public List<MemberLog> History { get; set; } = [];
    public string PlanText { get; set; } = "";
    public int OwnedStories { get; set; }
    public int ReadingStories { get; set; }
    public int OrderCount { get; set; }
    public int? CommentCount { get; set; }
}

// PB35 + PB36 — quản lý thành viên: xem danh sách, thông tin sử dụng, hạn chế / tạm ngừng / khôi phục
[Area("Admin")]
public class MembersController(IWebHostEnvironment environment, DemoMemberStore members, DemoPlanStore orders, DemoReadingProgressStore progress)
    : PreviewController(environment)
{
    private const int PageSize = 10;
    private string AdminId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string AdminName => User.Identity?.Name ?? "Admin";

    public IActionResult Index(string? q, string? state, string? role, int page = 1)
    {
        var (items, total) = members.Search(q, state, role, Math.Max(1, page), PageSize);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        if (page > pageCount) (items, total) = members.Search(q, state, role, page = pageCount, PageSize);
        return View(new MembersIndexVm
        {
            Items = items, Q = q, State = state, Role = role, Page = Math.Max(1, page), PageCount = pageCount, Total = total,
            States = items.ToDictionary(m => m.Id, m => members.StateOf(m.Id))
        });
    }

    public IActionResult Detail(string id)
    {
        var m = members.Get(id);
        if (m is null) { TempData["Toast"] = "Không tìm thấy thành viên."; return RedirectToAction(nameof(Index)); }
        var (state, info) = members.StateOf(id);
        var vm = new MemberDetailVm { Member = m, State = state, Restriction = info, History = members.History(id) };
        if (m.IsSample)
        {
            vm.PlanText = m.SamplePlan ?? "Chưa đăng ký gói";
            vm.OwnedStories = m.SampleOwned; vm.ReadingStories = m.SampleReading; vm.CommentCount = m.SampleComments;
            vm.OrderCount = m.SampleOwned + (m.SamplePlan is null ? 0 : 1);
        }
        else
        {
            var plans = orders.PlanEntitlements(id);
            vm.PlanText = plans.Count == 0 ? "Chưa đăng ký gói" : string.Join(", ", plans.Select(p => $"{p.PlanName} (hết hạn {p.ExpiresAt.ToLocalTime():dd/MM/yyyy})"));
            vm.OwnedStories = orders.OwnedStories(id).Count;
            vm.OrderCount = orders.Orders(id).Count;
            vm.ReadingStories = progress.Count(id);
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Restrict(string id, string kind, string? reason, int? days)
    {
        var k = kind == "Suspended" ? MemberState.Suspended : MemberState.Restricted;
        var error = members.Apply(id, AdminId, AdminName, k, reason, days);
        TempData["Toast"] = error ?? (k == MemberState.Suspended ? "Đã tạm ngừng tài khoản." : "Đã hạn chế bình luận của tài khoản.");
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Restore(string id)
    {
        TempData["Toast"] = members.Restore(id, AdminName) ?? "Đã khôi phục tài khoản.";
        return RedirectToAction(nameof(Detail), new { id });
    }
}

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Areas.Admin.Controllers;

// PB37–PB40 — Dashboard thống kê cho Admin: bộ lọc thời gian, bảng, biểu đồ, trạng thái rỗng/lỗi.
//   /Admin/Statistics/Activity  PB37  hoạt động thành viên, đăng ký/gia hạn/mua truyện, trạng thái giao dịch
//   /Admin/Statistics/Revenue   PB38 + PB39  doanh thu theo gói và theo truyện
//   /Admin/Statistics/Content   PB40  lượt đọc, theo dõi, mua, quan tâm theo truyện/chương/thể loại
[Area("Admin")]
public class StatisticsController(IWebHostEnvironment environment, StatisticsApiClient api) : PreviewController(environment)
{
    private Task<string?> Token() => HttpContext.GetTokenAsync("access_token");
    private static readonly string[] Sorts = ["interest", "reads", "follows", "purchases", "comments"];

    public IActionResult Index() => RedirectToAction(nameof(Activity));

    public async Task<IActionResult> Activity(string? from, string? to, string? preset)
    {
        var f = StatsFilter.Resolve(from, to, preset); var token = await Token();
        var overview = api.ActivityOverviewAsync(f.From, f.To, token);
        var daily = api.ActivityDailyAsync(f.From, f.To, token);
        await Task.WhenAll(overview, daily);
        return View(new ActivityStatsVm { Filter = f, Overview = overview.Result, Daily = daily.Result });
    }

    public async Task<IActionResult> Revenue(string? from, string? to, string? preset)
    {
        var f = StatsFilter.Resolve(from, to, preset);
        return View(new RevenueStatsVm { Filter = f, Report = await api.RevenueAsync(f.From, f.To, await Token()) });
    }

    public async Task<IActionResult> Content(string? from, string? to, string? preset, string? sort)
    {
        var f = StatsFilter.Resolve(from, to, preset); var token = await Token();
        sort = Sorts.Contains(sort) ? sort! : "interest";
        var overview = api.ContentOverviewAsync(f.From, f.To, token);
        var daily = api.ContentDailyAsync(f.From, f.To, token);
        var stories = api.ContentStoriesAsync(f.From, f.To, sort, 10, token);
        var genres = api.ContentGenresAsync(f.From, f.To, token);
        await Task.WhenAll(overview, daily, stories, genres);
        return View(new ContentStatsVm { Filter = f, Sort = sort, Overview = overview.Result, Daily = daily.Result, Stories = stories.Result, Genres = genres.Result });
    }

    // Chi tiết từng chương của 1 truyện (xem chương nào được đọc / bàn luận nhiều)
    public async Task<IActionResult> Chapters(int storyId, string? title, string? from, string? to, string? preset)
    {
        var f = StatsFilter.Resolve(from, to, preset);
        return View(new ChapterStatsVm { Filter = f, StoryId = storyId, StoryTitle = title,
            Chapters = await api.ContentChaptersAsync(storyId, f.From, f.To, await Token()) });
    }

    // PB38 — xuất báo cáo doanh thu CSV (UTF-8 có BOM để Excel đọc đúng tiếng Việt)
    public async Task<IActionResult> ExportRevenue(string? from, string? to, string? preset)
    {
        var f = StatsFilter.Resolve(from, to, preset);
        var r = await api.RevenueAsync(f.From, f.To, await Token());
        if (!r.Ok) { TempData["Toast"] = r.Error; return RedirectToAction(nameof(Revenue), new { from = f.FromText, to = f.ToText }); }

        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Mã đơn,Thời gian thanh toán (giờ VN),Loại,Nội dung,Thành viên,Số tiền (đ)\r\n");
        foreach (var o in r.Data!.Orders)
            sb.Append(o.OrderId).Append(',')
              .Append(DateTime.SpecifyKind(o.PaidAt!.Value, DateTimeKind.Utc).AddHours(7).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(',')
              .Append(o.OrderType == "Plan" ? "Gói đọc" : "Mua truyện").Append(',')
              .Append(Q(o.ItemName)).Append(',').Append(Q(o.UserName ?? "")).Append(',')
              .Append(o.Amount.ToString("0", CultureInfo.InvariantCulture)).Append("\r\n");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"doanh-thu_{f.FromText}_{f.ToText}.csv");
    }
}

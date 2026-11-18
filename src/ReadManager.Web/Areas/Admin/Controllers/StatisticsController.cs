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

    public async Task<IActionResult> Revenue(string? from, string? to, string? preset, string? type, int? planVersionId, int? storyId, string? groupBy)
    {
        var f = StatsFilter.Resolve(from, to, preset); var rf = RevenueFilter.Resolve(type, planVersionId, storyId, groupBy);
        var token = await Token(); var q = rf.ToQuery(f);
        var overview = api.RevenueOverviewAsync(q, token);
        var series = api.RevenueSeriesAsync(q, token);
        var plans = api.RevenuePlansAsync(q, token);
        var stories = api.RevenueStoriesAsync(q, 20, token);
        var options = api.RevenueFiltersAsync(token);
        await Task.WhenAll(overview, series, plans, stories, options);
        return View(new RevenueStatsVm
        {
            Filter = f,
            Rev = rf,
            Options = options.Result,
            Overview = overview.Result,
            Series = series.Result,
            Plans = plans.Result,
            Stories = stories.Result
        });
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
        return View(new ChapterStatsVm
        {
            Filter = f,
            StoryId = storyId,
            StoryTitle = title,
            Chapters = await api.ContentChaptersAsync(storyId, f.From, f.To, await Token())
        });
    }

    // PB38 — xuất báo cáo doanh thu CSV: API dựng file (UTF-8 có BOM để Excel đọc đúng tiếng Việt), Web chuyển tiếp cho trình duyệt
    public async Task<IActionResult> ExportRevenue(string? from, string? to, string? preset, string? type, int? planVersionId, int? storyId, string? groupBy)
    {
        var f = StatsFilter.Resolve(from, to, preset); var rf = RevenueFilter.Resolve(type, planVersionId, storyId, groupBy);
        var r = await api.RevenueCsvAsync(rf.ToQuery(f), await Token());
        if (!r.Ok)
        {
            TempData["Toast"] = r.Error;
            return RedirectToAction(nameof(Revenue), new { from = f.FromText, to = f.ToText, type = rf.Type, planVersionId = rf.PlanVersionId, storyId = rf.StoryId, groupBy = rf.GroupBy });
        }
        return File(r.Data!.Content, "text/csv; charset=utf-8", r.Data.FileName);
    }
}

using ReadManager.Web.Services;

namespace ReadManager.Web.ViewModels;

// Bộ lọc thời gian dùng chung cho mọi tab thống kê
public class StatsFilter
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string? Preset { get; set; }          // today | 7 | 30 | 90 | month
    public string? Error { get; set; }           // nhập sai khoảng ngày → đã quay về mặc định
    public string? Note { get; set; }            // ví dụ: ngày kết thúc bị đưa về hôm nay
    public string FromText => From.ToString("yyyy-MM-dd");
    public string ToText => To.ToString("yyyy-MM-dd");
    public int Days => To.DayNumber - From.DayNumber + 1;
    public string Label => $"{From:dd/MM/yyyy} – {To:dd/MM/yyyy} ({Days} ngày)";

    public static DateOnly TodayVn => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    // Đọc from/to/preset từ query string. Sai thì báo lỗi và dùng 30 ngày gần nhất.
    public static StatsFilter Resolve(string? from, string? to, string? preset)
    {
        var today = TodayVn;
        StatsFilter Make(DateOnly f, DateOnly t, string? p = null, string? err = null, string? note = null)
            => new() { From = f, To = t, Preset = p, Error = err, Note = note };
        var def = Make(today.AddDays(-29), today, "30");

        switch (preset)
        {
            case "today": return Make(today, today, "today");
            case "7": return Make(today.AddDays(-6), today, "7");
            case "30": return def;
            case "90": return Make(today.AddDays(-89), today, "90");
            case "month": return Make(new DateOnly(today.Year, today.Month, 1), today, "month");
        }
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)) return def;

        string fallback(string msg) { def.Error = msg; return ""; }
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) { fallback("Vui lòng chọn đủ “Từ ngày” và “Đến ngày”."); return def; }
        if (!DateOnly.TryParse(from, out var f) || !DateOnly.TryParse(to, out var t)) { fallback("Ngày không hợp lệ. Hãy chọn lại bằng ô chọn ngày."); return def; }
        if (f > t) { fallback("“Từ ngày” phải trước hoặc bằng “Đến ngày”."); return def; }
        string? note = null;
        if (t > today) { t = today; note = "Ngày kết thúc đã được đưa về hôm nay (không có dữ liệu tương lai)."; }
        if (f > t) { fallback("“Từ ngày” không được ở tương lai."); return def; }
        if (t.DayNumber - f.DayNumber >= 366) { fallback("Chỉ xem được tối đa 366 ngày mỗi lần."); return def; }
        return Make(f, t, null, null, note);
    }
}

public class ActivityStatsVm
{
    public StatsFilter Filter { get; set; } = null!;
    public StatResult<ActivityOverviewApi> Overview { get; set; } = null!;
    public StatResult<List<ActivityDailyApi>> Daily { get; set; } = null!;
}

// PB38 + PB39 — bộ lọc riêng của tab Doanh thu (loại / gói / truyện / cách gom biểu đồ), đi kèm StatsFilter (khoảng ngày)
public class RevenueFilter
{
    public string Type { get; set; } = "all";          // all | plan | story
    public int? PlanVersionId { get; set; }
    public int? StoryId { get; set; }
    public string GroupBy { get; set; } = "day";       // day | week | month
    public string? Note { get; set; }

    public RevenueQuery ToQuery(StatsFilter f) => new(f.From, f.To, Type, PlanVersionId, StoryId, GroupBy);

    // Đọc tham số từ query string, giá trị sai thì về mặc định. Chọn cụ thể 1 gói / 1 truyện thì loại tự khớp theo.
    public static RevenueFilter Resolve(string? type, int? planVersionId, int? storyId, string? groupBy)
    {
        var r = new RevenueFilter
        {
            Type = (type is "plan" or "story") ? type : "all",
            GroupBy = (groupBy is "week" or "month") ? groupBy : "day",
            PlanVersionId = planVersionId > 0 ? planVersionId : null,
            StoryId = storyId > 0 ? storyId : null
        };
        if (r.PlanVersionId is not null && r.StoryId is not null)
        {
            r.StoryId = null;
            r.Note = "Chỉ lọc được một gói hoặc một truyện mỗi lần. Đang lọc theo gói đã chọn.";
        }
        if (r.PlanVersionId is not null) r.Type = "plan";
        else if (r.StoryId is not null) r.Type = "story";
        if (r.Type == "plan") r.StoryId = null;
        if (r.Type == "story") r.PlanVersionId = null;
        return r;
    }
}

public class RevenueStatsVm
{
    public StatsFilter Filter { get; set; } = null!;
    public RevenueFilter Rev { get; set; } = null!;
    public StatResult<RevenueFilterOptionsApi> Options { get; set; } = null!;
    public StatResult<RevenueOverviewApi> Overview { get; set; } = null!;
    public StatResult<List<RevenueBucketApi>> Series { get; set; } = null!;
    public StatResult<List<PlanRevenueApi>> Plans { get; set; } = null!;
    public StatResult<List<StoryRevenueApi>> Stories { get; set; } = null!;
}

public class ContentStatsVm
{
    public StatsFilter Filter { get; set; } = null!;
    public string Sort { get; set; } = "interest";
    public StatResult<ContentOverviewApi> Overview { get; set; } = null!;
    public StatResult<List<ContentDailyApi>> Daily { get; set; } = null!;
    public StatResult<List<ContentStoryStatApi>> Stories { get; set; } = null!;
    public StatResult<List<ContentGenreStatApi>> Genres { get; set; } = null!;
}

public class ChapterStatsVm
{
    public StatsFilter Filter { get; set; } = null!;
    public int StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public StatResult<List<ContentChapterStatApi>> Chapters { get; set; } = null!;
}
public record KpiVm(string Label, string Value, string? Sub = null, string? Tone = null);

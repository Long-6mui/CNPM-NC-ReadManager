using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Statistics.Revenue;

namespace ReadManager.Api.Services;

// BACKEND 4 — PB38 + PB39: THỐNG KÊ DOANH THU (gói đọc, mua truyện) THEO NGÀY / TUẦN / THÁNG
//
// QUY TẮC TÍNH DOANH THU (đổi ở đây là đổi cho toàn bộ thống kê doanh thu):
//   1. Chỉ tính đơn Orders.Status = "Paid" và có PaidAt. Đơn Pending / Failed / Cancelled / Expired chưa trả tiền thì không tính.
//   2. Tính theo NGÀY THANH TOÁN (PaidAt, giờ Việt Nam UTC+7), không phải ngày tạo đơn. Tuần bắt đầu từ thứ Hai.
//   3. Số tiền = Orders.Amount (đã chốt lúc tạo đơn và được PaymentService đối chiếu với cổng thanh toán khi callback).
//      TUYỆT ĐỐI không lấy giá hiện tại của ReadingPlanVersions / Stories.
//   4. Chỉ đọc bảng Orders (mỗi OrderId 1 dòng) nên 1 đơn có nhiều lượt Payments vẫn chỉ tính 1 lần. KHÔNG join Payments.
//   5. Đã thanh toán nhưng cấp quyền đọc lỗi (thiếu UserSubscription / StoryPurchase) vẫn tính doanh thu.
//   6. Gói đã hết hạn / ngừng bán, truyện bị ẩn: doanh thu lịch sử vẫn giữ (không lọc IsAvailable, Visibility).
//   7. Gói chứa nhiều truyện: toàn bộ tiền thuộc doanh thu GÓI, không chia thêm vào doanh thu từng truyện.
//   8. Gia hạn vẫn là doanh thu gói, nhưng tách được "mua mới" và "gia hạn" (xem PlanOrderClassifier — dùng chung với PB37).
//   9. Mốc ngày/tuần/tháng không có giao dịch trả 0 để biểu đồ không bị đứt.
public interface IRevenueStatisticsService
{
    // Các hàm dưới đây ném ArgumentException (→ 400) nếu tham số sai, KeyNotFoundException (→ 404) nếu gói / truyện không tồn tại
    Task<RevenueOverviewDto> GetOverviewAsync(RevenueStatsQuery query);
    Task<List<RevenueBucketDto>> GetSeriesAsync(RevenueStatsQuery query);
    Task<List<PlanRevenueDto>> GetPlansAsync(RevenueStatsQuery query);
    Task<List<StoryRevenueDto>> GetStoriesAsync(RevenueStatsQuery query);
    Task<RevenueFilterOptionsDto> GetFilterOptionsAsync();
    Task<RevenueCsvFile> ExportCsvAsync(RevenueStatsQuery query);
}

public sealed record RevenueCsvFile(byte[] Content, string FileName);

public class RevenueStatisticsService : IRevenueStatisticsService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    private const string StatusPaid = "Paid";
    private const string TypePlan = "Plan";
    private const string TypeStory = "Story";

    private static readonly TimeZoneInfo VietnamTime = FindVietnamTimeZone();

    private readonly AppDbContext _db;

    public RevenueStatisticsService(AppDbContext db)
    {
        _db = db;
    }

    // =====================================================================
    // 1. TỔNG QUAN
    // =====================================================================
    public async Task<RevenueOverviewDto> GetOverviewAsync(RevenueStatsQuery query)
    {
        var f = await ResolveAsync(query);
        var rows = await LoadRowsAsync(f);
        var renewals = await FindRenewalIdsAsync(rows.Select(r => new RevenueOrder(r.OrderId, r.UserId, r.OrderType, r.PlanId, r.PaidAt)));

        var plan = rows.Where(r => r.OrderType == TypePlan).ToList();
        var story = rows.Where(r => r.OrderType == TypeStory).ToList();
        var renewalRows = plan.Where(r => renewals.Contains(r.OrderId)).ToList();
        var newRows = plan.Where(r => !renewals.Contains(r.OrderId)).ToList();
        var total = rows.Sum(r => r.Amount);

        return new RevenueOverviewDto
        {
            From = f.Range.From,
            To = f.Range.To,
            Type = f.Type,
            TotalRevenue = total,
            OrderCount = rows.Count,
            AverageOrderValue = rows.Count == 0 ? 0 : Math.Round(total / rows.Count, 2),
            PlanRevenue = plan.Sum(r => r.Amount),
            PlanOrders = plan.Count,
            NewPlanRevenue = newRows.Sum(r => r.Amount),
            NewPlanOrders = newRows.Count,
            RenewalRevenue = renewalRows.Sum(r => r.Amount),
            RenewalOrders = renewalRows.Count,
            StoryRevenue = story.Sum(r => r.Amount),
            StoryOrders = story.Count
        };
    }

    // =====================================================================
    // 2. THEO NGÀY / TUẦN / THÁNG (biểu đồ)
    // =====================================================================
    public async Task<List<RevenueBucketDto>> GetSeriesAsync(RevenueStatsQuery query)
    {
        var f = await ResolveAsync(query);
        var rows = await LoadRowsAsync(f);

        // Chia theo NGÀY GIỜ VIỆT NAM rồi gom vào mốc ngày / tuần / tháng (làm trong bộ nhớ cho đúng múi giờ)
        var byBucket = rows
            .GroupBy(r => BucketStart(VnDate(r.PaidAt), f.GroupBy))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Trả đủ mọi mốc trong khoảng (mốc không có giao dịch → 0) để biểu đồ không bị đứt
        var result = new List<RevenueBucketDto>();
        for (var b = BucketStart(f.Range.From, f.GroupBy); b <= f.Range.To; b = NextBucket(b, f.GroupBy))
        {
            var list = byBucket.GetValueOrDefault(b) ?? new List<Row>();
            var planRevenue = list.Where(r => r.OrderType == TypePlan).Sum(r => r.Amount);
            var storyRevenue = list.Where(r => r.OrderType == TypeStory).Sum(r => r.Amount);
            var end = NextBucket(b, f.GroupBy).AddDays(-1);

            result.Add(new RevenueBucketDto
            {
                PeriodStart = b < f.Range.From ? f.Range.From : b,
                PeriodEnd = end > f.Range.To ? f.Range.To : end,
                PlanRevenue = planRevenue,
                StoryRevenue = storyRevenue,
                TotalRevenue = planRevenue + storyRevenue,
                Orders = list.Count
            });
        }
        return result;
    }

    // =====================================================================
    // 3. THEO PHIÊN BẢN GÓI ĐỌC (PB38)
    // =====================================================================
    public async Task<List<PlanRevenueDto>> GetPlansAsync(RevenueStatsQuery query)
    {
        var f = await ResolveAsync(query);
        var rows = (await LoadRowsAsync(f)).Where(r => r.OrderType == TypePlan && r.PlanVersionId != null).ToList();
        if (rows.Count == 0) return new List<PlanRevenueDto>();

        var renewals = await FindRenewalIdsAsync(rows.Select(r => new RevenueOrder(r.OrderId, r.UserId, r.OrderType, r.PlanId, r.PaidAt)));

        var versionIds = rows.Select(r => r.PlanVersionId!.Value).Distinct().ToList();
        var versions = await _db.ReadingPlanVersions.AsNoTracking()
            .Where(v => versionIds.Contains(v.PlanVersionId))
            .Select(v => new { v.PlanVersionId, v.PlanId, PlanName = v.Plan.Name, v.VersionNumber, v.DurationMonths })
            .ToDictionaryAsync(v => v.PlanVersionId);

        var totalPlanRevenue = rows.Sum(r => r.Amount);

        return rows
            .GroupBy(r => r.PlanVersionId!.Value)
            .Select(g =>
            {
                var v = versions[g.Key];
                var renewalRows = g.Where(r => renewals.Contains(r.OrderId)).ToList();
                var newRows = g.Where(r => !renewals.Contains(r.OrderId)).ToList();
                var revenue = g.Sum(r => r.Amount);
                return new PlanRevenueDto
                {
                    PlanVersionId = g.Key,
                    PlanId = v.PlanId,
                    PlanName = v.PlanName,
                    VersionNumber = v.VersionNumber,
                    DurationMonths = v.DurationMonths,
                    Orders = g.Count(),
                    NewOrders = newRows.Count,
                    RenewalOrders = renewalRows.Count,
                    Revenue = revenue,
                    NewRevenue = newRows.Sum(r => r.Amount),
                    RenewalRevenue = renewalRows.Sum(r => r.Amount),
                    SharePercent = totalPlanRevenue > 0 ? Math.Round((double)(revenue / totalPlanRevenue * 100), 2) : 0
                };
            })
            .OrderByDescending(p => p.Revenue).ThenBy(p => p.PlanName).ThenBy(p => p.VersionNumber)
            .ToList();
    }

    // =====================================================================
    // 4. THEO TRUYỆN (PB39) — chỉ đơn mua truyện
    // =====================================================================
    public async Task<List<StoryRevenueDto>> GetStoriesAsync(RevenueStatsQuery query)
    {
        var f = await ResolveAsync(query);
        var top = Math.Clamp(query.Top, 1, 100);
        var rows = (await LoadRowsAsync(f)).Where(r => r.OrderType == TypeStory && r.StoryId != null).ToList();
        if (rows.Count == 0) return new List<StoryRevenueDto>();

        var storyIds = rows.Select(r => r.StoryId!.Value).Distinct().ToList();
        var titles = await _db.Stories.AsNoTracking()
            .Where(s => storyIds.Contains(s.StoryId))
            .Select(s => new { s.StoryId, s.Title })
            .ToDictionaryAsync(s => s.StoryId, s => s.Title);

        var totalStoryRevenue = rows.Sum(r => r.Amount);

        return rows
            .GroupBy(r => r.StoryId!.Value)
            .Select(g =>
            {
                var revenue = g.Sum(r => r.Amount);
                return new StoryRevenueDto
                {
                    StoryId = g.Key,
                    Title = titles.GetValueOrDefault(g.Key) ?? $"Truyện #{g.Key}",
                    Purchases = g.Count(),
                    Revenue = revenue,
                    SharePercent = totalStoryRevenue > 0 ? Math.Round((double)(revenue / totalStoryRevenue * 100), 2) : 0
                };
            })
            .OrderByDescending(s => s.Revenue).ThenBy(s => s.Title)
            .Take(top)
            .ToList();
    }

    // =====================================================================
    // 5. DANH SÁCH CHO Ô CHỌN GÓI / TRUYỆN (mọi thời điểm, chỉ những cái từng có đơn Paid)
    // =====================================================================
    public async Task<RevenueFilterOptionsDto> GetFilterOptionsAsync()
    {
        var paid = _db.Orders.AsNoTracking().Where(o => o.Status == StatusPaid && o.PaidAt != null);

        var versionIds = await paid.Where(o => o.PlanVersionId != null)
            .Select(o => o.PlanVersionId!.Value).Distinct().ToListAsync();
        var plans = await _db.ReadingPlanVersions.AsNoTracking()
            .Where(v => versionIds.Contains(v.PlanVersionId))
            .Select(v => new RevenuePlanOptionDto
            {
                PlanVersionId = v.PlanVersionId,
                PlanId = v.PlanId,
                PlanName = v.Plan.Name,
                VersionNumber = v.VersionNumber,
                DurationMonths = v.DurationMonths
            })
            .ToListAsync();

        var storyIds = await paid.Where(o => o.StoryId != null)
            .Select(o => o.StoryId!.Value).Distinct().ToListAsync();
        var stories = await _db.Stories.AsNoTracking()
            .Where(s => storyIds.Contains(s.StoryId))
            .Select(s => new RevenueStoryOptionDto { StoryId = s.StoryId, Title = s.Title })
            .ToListAsync();

        return new RevenueFilterOptionsDto
        {
            Plans = plans.OrderBy(p => p.PlanName).ThenBy(p => p.VersionNumber).ToList(),
            Stories = stories.OrderBy(s => s.Title).ToList()
        };
    }

    // =====================================================================
    // 6. XUẤT CSV (UTF-8 BOM để Excel đọc đúng tiếng Việt)
    // =====================================================================
    public async Task<RevenueCsvFile> ExportCsvAsync(RevenueStatsQuery query)
    {
        var f = await ResolveAsync(query);
        var rows = await PaidOrders(f)
            .OrderBy(o => o.PaidAt).ThenBy(o => o.OrderId)
            .Select(o => new ExportRow(
                o.OrderId, o.UserId, o.OrderType,
                o.PlanVersion != null ? (int?)o.PlanVersion.PlanId : null,
                o.PaidAt!.Value, o.Amount, o.ItemName, o.User.DisplayName))
            .ToListAsync();

        var renewals = await FindRenewalIdsAsync(rows.Select(r => new RevenueOrder(r.OrderId, r.UserId, r.OrderType, r.PlanId, r.PaidAt)));

        var sb = new StringBuilder();
        sb.Append("Mã đơn,Thời gian thanh toán (giờ VN),Loại,Mua mới / Gia hạn,Nội dung,Thành viên,Số tiền (đ)\r\n");
        foreach (var r in rows)
        {
            var vn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.PaidAt, DateTimeKind.Utc), VietnamTime);
            var kind = r.OrderType == TypePlan ? "Gói đọc" : "Mua truyện";
            var detail = r.OrderType == TypePlan ? (renewals.Contains(r.OrderId) ? "Gia hạn" : "Mua mới") : "";
            sb.Append(r.OrderId).Append(',')
              .Append(vn.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(',')
              .Append(kind).Append(',')
              .Append(detail).Append(',')
              .Append(Csv(r.ItemName)).Append(',')
              .Append(Csv(r.UserName)).Append(',')
              .Append(r.Amount.ToString("0", CultureInfo.InvariantCulture)).Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        return new RevenueCsvFile(bytes, $"doanh-thu_{f.Range.From:yyyy-MM-dd}_{f.Range.To:yyyy-MM-dd}.csv");
    }

    // =====================================================================
    // HÀM PHỤ
    // =====================================================================

    // Đơn đã thanh toán trong khoảng (theo PaidAt) + các bộ lọc. Chỉ đọc bảng Orders.
    private IQueryable<Entities.Order> PaidOrders(Filter f)
    {
        var orders = _db.Orders.AsNoTracking()
            .Where(o => o.Status == StatusPaid && o.PaidAt != null
                     && o.PaidAt >= f.Range.FromUtc && o.PaidAt < f.Range.ToUtc);

        if (f.Type == "plan") orders = orders.Where(o => o.OrderType == TypePlan);
        if (f.Type == "story") orders = orders.Where(o => o.OrderType == TypeStory);
        if (f.PlanVersionId is int pv) orders = orders.Where(o => o.PlanVersionId == pv);
        if (f.StoryId is int s) orders = orders.Where(o => o.StoryId == s);
        return orders;
    }

    private Task<List<Row>> LoadRowsAsync(Filter f)
        => PaidOrders(f)
            .OrderBy(o => o.PaidAt).ThenBy(o => o.OrderId)
            .Select(o => new Row(
                o.OrderId, o.UserId, o.OrderType, o.PlanVersionId,
                o.PlanVersion != null ? (int?)o.PlanVersion.PlanId : null,
                o.StoryId, o.Amount, o.PaidAt!.Value))
            .ToListAsync();

    // Tập OrderId là "gia hạn" trong số các đơn gói truyền vào (luật dùng chung với PB37)
    private async Task<HashSet<int>> FindRenewalIdsAsync(IEnumerable<RevenueOrder> orders)
    {
        var plan = orders.Where(o => o.OrderType == TypePlan && o.PlanId != null).ToList();
        if (plan.Count == 0) return new HashSet<int>();

        var userIds = plan.Select(o => o.UserId).Distinct().ToList();
        var grants = await PlanOrderClassifier.LoadGrantsAsync(_db, userIds);
        return PlanOrderClassifier.FindRenewalOrderIds(
            plan.Select(o => new PlanOrderClassifier.PlanOrder(o.OrderId, o.UserId, o.PlanId!.Value, o.PaidAt)),
            grants);
    }

    // Kiểm tra tham số, chuẩn hóa bộ lọc
    private async Task<Filter> ResolveAsync(RevenueStatsQuery q)
    {
        var range = ResolveRange(q);

        var type = (q.Type ?? "all").Trim().ToLowerInvariant();
        if (type is not ("all" or "plan" or "story"))
            throw new ArgumentException("type phải là all, plan hoặc story.");

        var groupBy = (q.GroupBy ?? "day").Trim().ToLowerInvariant();
        if (groupBy is not ("day" or "week" or "month"))
            throw new ArgumentException("groupBy phải là day, week hoặc month.");

        if (q.PlanVersionId is not null && q.StoryId is not null)
            throw new ArgumentException("Chỉ lọc theo một gói hoặc một truyện, không chọn cả hai cùng lúc.");

        if (q.PlanVersionId is int pv)
        {
            if (pv <= 0) throw new ArgumentException("planVersionId không hợp lệ.");
            if (type == "story") throw new ArgumentException("Lọc theo gói thì type phải là all hoặc plan.");
            if (!await _db.ReadingPlanVersions.AnyAsync(v => v.PlanVersionId == pv))
                throw new KeyNotFoundException("Không tìm thấy gói đọc này.");
            type = "plan";
        }

        if (q.StoryId is int sid)
        {
            if (sid <= 0) throw new ArgumentException("storyId không hợp lệ.");
            if (type == "plan") throw new ArgumentException("Lọc theo truyện thì type phải là all hoặc story.");
            if (!await _db.Stories.AnyAsync(s => s.StoryId == sid))
                throw new KeyNotFoundException("Không tìm thấy truyện này.");
            type = "story";
        }

        return new Filter(range, type, q.PlanVersionId, q.StoryId, groupBy);
    }

    // Khoảng ngày (giờ VN) → khoảng giờ UTC [FromUtc, ToUtc) để so sánh với dữ liệu lưu UTC
    private static DateRange ResolveRange(RevenueStatsQuery query)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTime));
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-(DefaultDays - 1));

        if (from > to)
            throw new ArgumentException("Ngày bắt đầu (from) phải trước hoặc bằng ngày kết thúc (to).");
        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"Chỉ xem tối đa {MaxDays} ngày mỗi lần.");

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), VietnamTime);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamTime);
        return new DateRange(from, to, fromUtc, toUtc);
    }

    private static DateOnly VnDate(DateTime utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTime));

    // Ngày đầu của mốc chứa ngày d: tuần bắt đầu từ THỨ HAI, tháng bắt đầu từ ngày 1
    private static DateOnly BucketStart(DateOnly d, string groupBy) => groupBy switch
    {
        "week" => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)),
        "month" => new DateOnly(d.Year, d.Month, 1),
        _ => d
    };

    private static DateOnly NextBucket(DateOnly start, string groupBy) => groupBy switch
    {
        "week" => start.AddDays(7),
        "month" => start.AddMonths(1),
        _ => start.AddDays(1)
    };

    // Chống CSV injection: tên thành viên do người dùng tự đặt, Excel sẽ chạy công thức nếu ô bắt đầu bằng = + - @
    private static string Csv(string s)
    {
        s ??= string.Empty;
        if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static TimeZoneInfo FindVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh", "Asia/Bangkok" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    private sealed record DateRange(DateOnly From, DateOnly To, DateTime FromUtc, DateTime ToUtc);
    private sealed record Filter(DateRange Range, string Type, int? PlanVersionId, int? StoryId, string GroupBy);
    private sealed record Row(int OrderId, int UserId, string OrderType, int? PlanVersionId, int? PlanId, int? StoryId, decimal Amount, DateTime PaidAt);
    private sealed record ExportRow(int OrderId, int UserId, string OrderType, int? PlanId, DateTime PaidAt, decimal Amount, string ItemName, string UserName);
    private sealed record RevenueOrder(int OrderId, int UserId, string OrderType, int? PlanId, DateTime PaidAt);
}

namespace ReadManager.Web.Services;

public record RevenueDay(DateOnly Date, decimal Plan, decimal Story, int Orders) { public decimal Total => Plan + Story; }
public record RevenueItem(string Name, int Orders, decimal Revenue);

// PB38 + PB39 — báo cáo doanh thu tính từ các đơn đã thanh toán.
// Quy tắc: chỉ tính đơn Paid có PaidAt; mỗi OrderId chỉ tính 1 lần (tránh tính trùng); ngày theo giờ Việt Nam (UTC+7).
public class RevenueReport
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public decimal Total { get; init; }
    public decimal PlanTotal { get; init; }
    public decimal StoryTotal { get; init; }
    public int OrderCount { get; init; }
    public int DuplicatesIgnored { get; init; }
    public bool Truncated { get; init; }
    public decimal AverageOrder => OrderCount == 0 ? 0 : Total / OrderCount;
    public List<RevenueDay> Days { get; init; } = [];
    public List<RevenueItem> ByPlan { get; init; } = [];     // PB38
    public List<RevenueItem> ByStory { get; init; } = [];    // PB39
    public List<OrderApi> Orders { get; init; } = [];

    public static DateOnly VnDate(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7));

    public static RevenueReport Build(IEnumerable<OrderApi> source, DateOnly from, DateOnly to, bool truncated = false)
    {
        var seen = new HashSet<int>();
        var counted = new List<OrderApi>();
        var dup = 0;
        foreach (var o in source)
        {
            if (o.Status != "Paid" || o.PaidAt is null) continue;
            var d = VnDate(o.PaidAt.Value);
            if (d < from || d > to) continue;
            if (!seen.Add(o.OrderId)) { dup++; continue; }
            counted.Add(o);
        }

        var days = new List<RevenueDay>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var day = counted.Where(o => VnDate(o.PaidAt!.Value) == d).ToList();
            days.Add(new RevenueDay(d, day.Where(o => o.OrderType == "Plan").Sum(o => o.Amount),
                                       day.Where(o => o.OrderType == "Story").Sum(o => o.Amount), day.Count));
        }

        return new RevenueReport
        {
            From = from, To = to, Truncated = truncated, DuplicatesIgnored = dup, OrderCount = counted.Count,
            Total = counted.Sum(o => o.Amount),
            PlanTotal = counted.Where(o => o.OrderType == "Plan").Sum(o => o.Amount),
            StoryTotal = counted.Where(o => o.OrderType == "Story").Sum(o => o.Amount),
            Days = days,
            ByPlan = Group(counted.Where(o => o.OrderType == "Plan"), o => o.PlanId ?? 0),
            ByStory = Group(counted.Where(o => o.OrderType == "Story"), o => o.StoryId ?? 0),
            Orders = counted.OrderByDescending(o => o.PaidAt).ToList()
        };
    }

    private static List<RevenueItem> Group(IEnumerable<OrderApi> orders, Func<OrderApi, int> key)
        => orders.GroupBy(key)
                 .Select(g => new RevenueItem(g.OrderByDescending(o => o.PaidAt).First().ItemName, g.Count(), g.Sum(o => o.Amount)))
                 .OrderByDescending(x => x.Revenue).ThenBy(x => x.Name).ToList();
}

using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Cách tạm khi chưa có API quyền đọc: suy ra từ các đơn hàng ĐÃ THANH TOÁN (GET api/me/orders?status=Paid).
public class OrderBasedReadingAccessService : IReadingAccessService
{
    private readonly IOrderService _orders;

    public OrderBasedReadingAccessService(IOrderService orders) => _orders = orders;

    public async Task<PurchaseSummary> GetPurchasesAsync()
    {
        var paid = new List<Order>();
        for (var page = 1; page <= 20; page++)   // tối đa 1000 đơn
        {
            var result = await _orders.GetMyOrdersAsync("Paid", page, 50);
            paid.AddRange(result.Items);
            if (page >= result.TotalPages) break;
        }

        var summary = new PurchaseSummary();

        // Truyện mua trọn bộ: vĩnh viễn
        foreach (var o in paid.Where(o => o.IsStory && o.StoryId.HasValue)
                              .OrderByDescending(o => o.PaidAt ?? o.CreatedAt)
                              .GroupBy(o => o.StoryId!.Value).Select(g => g.First()))
        {
            summary.Stories.Add(new OwnedStory
            {
                StoryId = o.StoryId!.Value,
                Title = o.ItemName,
                Amount = o.Amount,
                PurchasedAt = Fmt.Local(o.PaidAt ?? o.CreatedAt)
            });
        }

        // Gói đọc: mua lại khi còn hạn thì cộng dồn (giống web)
        var now = DateTime.UtcNow;
        foreach (var group in paid.Where(o => !o.IsStory)
                                  .GroupBy(o => o.PlanId ?? o.PlanVersionId ?? 0))
        {
            DateTime? expires = null;
            foreach (var o in group.OrderBy(o => o.PaidAt ?? o.CreatedAt))
            {
                var start = DateTime.SpecifyKind(o.PaidAt ?? o.CreatedAt, DateTimeKind.Utc);
                var from = expires.HasValue && expires.Value > start ? expires.Value : start;
                expires = from.AddMonths(o.DurationMonths ?? 1);
            }
            if (expires.HasValue && expires.Value > now)
            {
                summary.Plans.Add(new PlanEntitlement
                {
                    PlanId = group.Key,
                    PlanName = PurchaseSummary.PlanNameOf(group.Last().ItemName),
                    ExpiresAt = expires.Value.ToLocalTime()
                });
            }
        }
        summary.Plans = summary.Plans.OrderBy(p => p.ExpiresAt).ToList();
        return summary;
    }
}

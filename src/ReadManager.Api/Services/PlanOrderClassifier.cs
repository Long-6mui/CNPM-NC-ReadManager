using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;

namespace ReadManager.Api.Services;

// PB37 + PB38 — MỘT luật duy nhất để phân loại đơn gói là "mua mới" hay "gia hạn", dùng chung cho
// ActivityStatisticsService (PB37) và RevenueStatisticsService (PB38) để 2 màn hình luôn ra cùng số.
//
// GIA HẠN = lúc thanh toán, chính thành viên đó vẫn còn quyền đọc (UserSubscription chưa bị thu hồi, EndsAt còn sau thời điểm thanh toán)
//           của một đơn gói ĐÃ THANH TOÁN TRƯỚC ĐÓ thuộc CÙNG MỘT GÓI (PlanId, mọi phiên bản của gói đó tính chung).
//           Đây cũng là luật EntitlementGrantService.AddGrantAsync dùng để nối tiếp hạn.
// MUA MỚI   = mọi đơn gói còn lại (lần đầu mua, mua lại sau khi hết hạn, mua gói khác).
public static class PlanOrderClassifier
{
    public sealed record PlanOrder(int OrderId, int UserId, int PlanId, DateTime PaidAtUtc);

    public sealed record PlanGrant(int OrderId, int UserId, int PlanId, DateTime PaidAtUtc, DateTime EndsAtUtc);

    // Trả về tập OrderId được xem là gia hạn trong số các đơn truyền vào
    public static HashSet<int> FindRenewalOrderIds(IEnumerable<PlanOrder> orders, IEnumerable<PlanGrant> grants)
    {
        var grantsByUserPlan = grants.ToLookup(g => (g.UserId, g.PlanId));
        var renewals = new HashSet<int>();

        foreach (var o in orders)
        {
            var isRenewal = grantsByUserPlan[(o.UserId, o.PlanId)].Any(g =>
                g.OrderId != o.OrderId
                && g.EndsAtUtc > o.PaidAtUtc
                // Chỉ xét đơn mua TRƯỚC. Thiếu điều kiện này, đơn mua sau sẽ làm đơn mua trước bị tính nhầm là gia hạn.
                && (g.PaidAtUtc < o.PaidAtUtc || (g.PaidAtUtc == o.PaidAtUtc && g.OrderId < o.OrderId)));
            if (isRenewal) renewals.Add(o.OrderId);
        }
        return renewals;
    }

    // Các quyền gói chưa bị thu hồi (userIds = null → của mọi thành viên). Thời điểm mua = PaidAt (nếu thiếu thì CreatedAt như PB37).
    public static async Task<List<PlanGrant>> LoadGrantsAsync(AppDbContext db, List<int>? userIds = null)
    {
        var query = db.UserSubscriptions.AsNoTracking()
            .Where(s => s.RevokedAt == null && s.Order.PlanVersion != null);
        if (userIds is not null)
            query = query.Where(s => userIds.Contains(s.Order.UserId));

        return await query
            .Select(s => new PlanGrant(
                s.OrderId,
                s.Order.UserId,
                s.Order.PlanVersion!.PlanId,
                s.Order.PaidAt ?? s.Order.CreatedAt,
                s.EndsAt))
            .ToListAsync();
    }
}

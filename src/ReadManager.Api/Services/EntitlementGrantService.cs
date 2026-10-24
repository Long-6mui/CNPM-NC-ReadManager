using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// BE4 — CẤP QUYỀN ĐỌC vào bảng : StoryPurchases (đơn Story) / UserSubscriptions (đơn Plan)
public interface IEntitlementGrantService
{
    // Thêm (CHƯA lưu) quyền đọc cho 1 đơn ĐÃ THANH TOÁN. Người gọi tự SaveChanges trong transaction của mình.
    // Trả về false nếu đơn này ĐÃ có quyền rồi (không thêm gì).
    Task<bool> AddGrantAsync(Order order, DateTime paidAtUtc);

    Task<bool> HasGrantAsync(int orderId);
}

public class EntitlementGrantService : IEntitlementGrantService
{
    private readonly AppDbContext _db;

    public EntitlementGrantService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasGrantAsync(int orderId)
        => await _db.StoryPurchases.AnyAsync(s => s.OrderId == orderId)
           || await _db.UserSubscriptions.AnyAsync(s => s.OrderId == orderId);

    public async Task<bool> AddGrantAsync(Order order, DateTime paidAtUtc)
    {
        // CHỐT AN TOÀN: chỉ đơn đã thanh toán mới được cấp quyền
        if (order.Status != OrderService.Paid)
            throw new InvalidOperationException($"Đơn {order.OrderId} chưa thanh toán nên không được cấp quyền.");

        // 1 đơn chỉ cấp 1 lần (DB cũng có unique OrderId làm lớp chặn cuối)
        if (await HasGrantAsync(order.OrderId))
            return false;

        if (order.OrderType == OrderService.TypeStory)
        {
            // Đơn Story → StoryPurchases: quyền riêng 1 truyện, độc lập với gói tháng
            if (order.StoryId is null)
                throw new InvalidOperationException($"Đơn {order.OrderId} thiếu StoryId.");

            _db.StoryPurchases.Add(new StoryPurchase
            {
                OrderId = order.OrderId,
                GrantedAt = paidAtUtc,
                ExpiresAt = null
            });
            return true;
        }

        if (order.OrderType == OrderService.TypePlan)
        {
            // Đơn Plan → UserSubscriptions: dùng số tháng + phiên bản gói đã chụp trong đơn
            if (order.PlanVersionId is null || order.DurationMonths is null)
                throw new InvalidOperationException($"Đơn {order.OrderId} thiếu phiên bản gói hoặc số tháng.");

            var planId = await _db.ReadingPlanVersions
                .AsNoTracking()
                .Where(v => v.PlanVersionId == order.PlanVersionId)
                .Select(v => v.PlanId)
                .FirstAsync();

            // Gia hạn khi gói còn hạn → nối tiếp từ hạn hiện có (không mất ngày còn lại);
            // gói đã hết hạn → bắt đầu từ lúc cấp mới. Bỏ qua quyền đã bị thu hồi.
            var latestEnd = await _db.UserSubscriptions
                .Where(s => s.RevokedAt == null
                            && s.EndsAt > paidAtUtc
                            && s.Order.UserId == order.UserId
                            && s.Order.PlanVersion != null
                            && s.Order.PlanVersion.PlanId == planId)
                .MaxAsync(s => (DateTime?)s.EndsAt);

            var startsAt = latestEnd ?? paidAtUtc;

            _db.UserSubscriptions.Add(new UserSubscription
            {
                OrderId = order.OrderId,
                StartsAt = startsAt,
                EndsAt = startsAt.AddMonths(order.DurationMonths.Value)
            });
            return true;
        }

        throw new InvalidOperationException($"Đơn {order.OrderId} có loại không hợp lệ: {order.OrderType}.");
    }
}
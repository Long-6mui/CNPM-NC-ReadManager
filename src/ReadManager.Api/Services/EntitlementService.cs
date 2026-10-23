using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// BE4 — CẤP QUYỀN ĐỌC (PB19, PB21)
public interface IEntitlementService
{
    // Tạo (CHƯA lưu) quyền đọc cho 1 đơn ĐÃ THANH TOÁN. Người gọi tự Add + SaveChanges
    // để lưu quyền cùng lúc với việc đánh dấu giao dịch "đã cấp quyền".
    Task<Entitlement> BuildForOrderAsync(Order order);
}

public class EntitlementService : IEntitlementService
{
    private readonly AppDbContext _db;

    public EntitlementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Entitlement> BuildForOrderAsync(Order order)
    {
        // CHỐT AN TOÀN (PB19/PB24): chỉ đơn đã thanh toán thành công mới được cấp quyền
        if (order.Status != OrderService.Paid)
            throw new InvalidOperationException(
                $"Đơn {order.OrderId} chưa thanh toán thành công nên không được cấp quyền.");

        // Tính từ lúc thanh toán (không phải lúc chạy lại) để người mua không mất ngày dùng khi hệ thống lỗi
        var paidAt = order.PaidAt ?? DateTime.UtcNow;

        var entitlement = new Entitlement
        {
            UserId = order.UserId,
            OrderId = order.OrderId,
            Source = order.OrderType,
            CreatedAt = DateTime.UtcNow
        };

        if (order.OrderType == OrderService.TypeStory)
        {
            // PB21: quyền đọc riêng 1 truyện — vĩnh viễn, độc lập với gói tháng
            entitlement.StoryId = order.StoryId;
            entitlement.StartsAt = paidAt;
            entitlement.ExpiresAt = null;
            return entitlement;
        }

        // PB19: gói đọc theo tháng — dùng số tháng + phiên bản gói đã chụp trong đơn (không lấy giá/thời hạn mới)
        if (order.PlanVersionId is null || order.DurationMonths is null)
            throw new InvalidOperationException($"Đơn {order.OrderId} thiếu phiên bản gói hoặc số tháng.");

        var planId = await _db.ReadingPlanVersions
            .AsNoTracking()
            .Where(v => v.PlanVersionId == order.PlanVersionId)
            .Select(v => v.PlanId)
            .FirstAsync();

        // Gia hạn khi gói còn hạn → cộng dồn: tính từ lúc gói cũ hết hạn
        var latestExpiry = await _db.Entitlements
            .Where(e => e.UserId == order.UserId
                        && e.Source == OrderService.TypePlan
                        && e.PlanVersion != null
                        && e.PlanVersion.PlanId == planId
                        && e.ExpiresAt > paidAt)
            .MaxAsync(e => e.ExpiresAt);

        var startsAt = latestExpiry ?? paidAt;

        entitlement.PlanVersionId = order.PlanVersionId;
        entitlement.StartsAt = startsAt;
        entitlement.ExpiresAt = startsAt.AddMonths(order.DurationMonths.Value);
        return entitlement;
    }
}
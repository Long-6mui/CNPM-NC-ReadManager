using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Orders;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

// BACKEND 3 — ĐƠN HÀNG
//   PB19: tạo đơn mua / gia hạn gói đọc
//   PB21: tạo đơn mua quyền đọc riêng 1 truyện
//   PB26: lịch sử thanh toán của đúng người dùng
public interface IOrderService
{
    // Báo giá trước khi mua. Trả null = không tìm thấy truyện / gói.
    Task<OrderQuoteDto?> QuoteStoryAsync(int storyId, int? userId);
    Task<OrderQuoteDto?> QuotePlanAsync(int planId, int? userId);

    // Tạo đơn. Trả null = không tìm thấy truyện / gói.
    // Ném ArgumentException (→ 400: dữ liệu sai / không bán) hoặc InvalidOperationException (→ 409: đã sở hữu).
    Task<OrderDto?> CreateAsync(int userId, CreateOrderDto dto);

    // PB26 — lịch sử của CHÍNH người đang đăng nhập
    Task<PagedResultDto<OrderDto>> GetMyOrdersAsync(int userId, string? status, int page, int pageSize);

    // Chi tiết 1 đơn. Người khác (không phải chủ đơn, không phải Admin) → null (404).
    Task<OrderDto?> GetByIdAsync(int orderId, int userId, bool isAdmin);

    // Admin xem tất cả đơn (lọc theo trạng thái / người dùng)
    Task<PagedResultDto<OrderDto>> GetAllAsync(string? status, int? userId, int page, int pageSize);

    // ----- Dành cho phần THANH TOÁN (BE4 — feature/s2-payments) gọi, KHÔNG mở API ra ngoài -----
    // Thanh toán thành công → Paid. Gọi lặp lại (callback lặp) không sao: trả false nếu đã Paid từ trước.
    Task<bool> MarkPaidAsync(int orderId, DateTime? paidAtUtc = null);
    // Thanh toán thất bại → Failed (chỉ đổi khi đơn đang Pending)
    Task<bool> MarkFailedAsync(int orderId);
}

public class OrderService : IOrderService
{
    public const string TypePlan = "Plan";
    public const string TypeStory = "Story";

    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";

    // Đơn chờ thanh toán quá 30 phút → hết hạn
    public static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(30);

    private const string Free = "Free";
    private const string PublicVisibility = "Public";
    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    // =====================================================================
    // BÁO GIÁ
    // =====================================================================
    public async Task<OrderQuoteDto?> QuoteStoryAsync(int storyId, int? userId)
    {
        var story = await _db.Stories.AsNoTracking()
            .Where(s => s.StoryId == storyId && s.Visibility == PublicVisibility)
            .Select(s => new { s.StoryId, s.Title, s.AccessPolicy, s.CurrentPrice })
            .FirstOrDefaultAsync();
        if (story is null)
            return null;

        var quote = new OrderQuoteDto
        {
            OrderType = TypeStory,
            StoryId = story.StoryId,
            ItemName = story.Title,
            Amount = story.CurrentPrice ?? 0
        };

        // Truyện miễn phí / chưa có giá → không bán
        if (story.AccessPolicy == Free || story.CurrentPrice is null || story.CurrentPrice <= 0)
        {
            quote.Message = "Truyện này miễn phí, không cần mua.";
            return quote;
        }

        if (userId.HasValue)
        {
            await ExpireOldPendingAsync(userId.Value);

            // Đã mua rồi → không bắt trả tiền lần nữa
            if (await OwnsStoryAsync(userId.Value, storyId))
            {
                quote.AlreadyOwned = true;
                quote.Message = "Bạn đã sở hữu truyện này.";
                return quote;
            }

            quote.PendingOrderId = await FindPendingAsync(userId.Value, TypeStory, storyId: storyId);
        }

        quote.CanBuy = true;
        return quote;
    }

    public async Task<OrderQuoteDto?> QuotePlanAsync(int planId, int? userId)
    {
        var plan = await _db.ReadingPlans.AsNoTracking().FirstOrDefaultAsync(p => p.PlanId == planId);
        if (plan is null)
            return null;

        var quote = new OrderQuoteDto { OrderType = TypePlan, PlanId = planId, ItemName = plan.Name };

        var version = plan.IsAvailable ? await CurrentVersionAsync(planId) : null;
        if (version is null)
        {
            quote.Message = "Gói này hiện không còn bán.";
            return quote;
        }

        quote.PlanVersionId = version.PlanVersionId;
        quote.Amount = version.Price;
        quote.DurationMonths = version.DurationMonths;
        quote.ItemName = PlanItemName(plan.Name, version.DurationMonths);

        if (userId.HasValue)
        {
            await ExpireOldPendingAsync(userId.Value);
            quote.PendingOrderId = await FindPendingAsync(userId.Value, TypePlan, planVersionId: version.PlanVersionId);
        }

        // Gói thì mua lại được (= gia hạn thêm), nên không chặn "đã sở hữu"
        quote.CanBuy = true;
        return quote;
    }

    // =====================================================================
    // TẠO ĐƠN
    // =====================================================================
    public async Task<OrderDto?> CreateAsync(int userId, CreateOrderDto dto)
    {
        // 1. Kiểm tra dữ liệu gửi lên: mua truyện thì phải có StoryId, mua gói thì phải có PlanId
        if (dto.OrderType == TypeStory && dto.StoryId is null)
            throw new ArgumentException("Mua truyện cần gửi StoryId.");
        if (dto.OrderType == TypePlan && dto.PlanId is null && dto.PlanVersionId is null)
            throw new ArgumentException("Mua gói cần gửi PlanId hoặc PlanVersionId.");

        // Mua gói theo PlanVersionId → tìm ra gói của phiên bản đó
        var planId = dto.PlanId;
        if (dto.OrderType == TypePlan && dto.PlanVersionId.HasValue)
        {
            planId = await _db.ReadingPlanVersions
                .Where(v => v.PlanVersionId == dto.PlanVersionId.Value)
                .Select(v => (int?)v.PlanId)
                .FirstOrDefaultAsync();
            if (planId is null)
                return null;
        }

        // 2. Báo giá — giá LẤY TỪ DATABASE, không tin giá client gửi
        var quote = dto.OrderType == TypeStory
            ? await QuoteStoryAsync(dto.StoryId!.Value, userId)
            : await QuotePlanAsync(planId!.Value, userId);
        if (quote is null)
            return null;

        // Người dùng đang xem phiên bản cũ (Admin vừa đổi giá) → không cho mua giá cũ
        if (dto.OrderType == TypePlan && dto.PlanVersionId.HasValue && quote.CanBuy
            && quote.PlanVersionId != dto.PlanVersionId)
            throw new ArgumentException("Giá gói vừa được cập nhật. Vui lòng tải lại trang để xem giá mới.");

        if (quote.AlreadyOwned)
            throw new InvalidOperationException(quote.Message ?? "Bạn đã sở hữu nội dung này.");
        if (!quote.CanBuy)
            throw new ArgumentException(quote.Message ?? "Không thể mua nội dung này.");

        // 3. Đang có đơn chờ thanh toán cho đúng món này → dùng lại đơn đó (bấm "Mua" 2 lần không tạo 2 đơn)
        if (quote.PendingOrderId.HasValue)
            return await GetByIdAsync(quote.PendingOrderId.Value, userId, isAdmin: false);

        // 4. Tạo đơn mới, chụp lại giá + tên + số tháng
        var now = DateTime.UtcNow;
        var order = new Order
        {
            UserId = userId,
            OrderType = quote.OrderType,
            StoryId = quote.StoryId,
            PlanVersionId = quote.PlanVersionId,
            ItemName = quote.ItemName,
            Amount = quote.Amount,
            DurationMonths = quote.DurationMonths,
            Status = Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(order.OrderId, userId, isAdmin: false);
    }

    // =====================================================================
    // XEM ĐƠN
    // =====================================================================
    public async Task<PagedResultDto<OrderDto>> GetMyOrdersAsync(int userId, string? status, int page, int pageSize)
    {
        await ExpireOldPendingAsync(userId);
        var query = _db.Orders.AsNoTracking().Where(o => o.UserId == userId);   // CHỈ đơn của mình
        return await PageAsync(query, status, page, pageSize, includeUserName: false);
    }

    public async Task<OrderDto?> GetByIdAsync(int orderId, int userId, bool isAdmin)
    {
        var owner = await _db.Orders.Where(o => o.OrderId == orderId).Select(o => (int?)o.UserId).FirstOrDefaultAsync();
        if (owner is null || (!isAdmin && owner != userId))
            return null;   // không lộ cho người khác biết đơn có tồn tại hay không

        await ExpireOldPendingAsync(owner.Value);

        var list = await ToDtos(_db.Orders.AsNoTracking().Where(o => o.OrderId == orderId), includeUserName: isAdmin)
            .ToListAsync();
        return list.Select(Finish).FirstOrDefault();
    }

    public async Task<PagedResultDto<OrderDto>> GetAllAsync(string? status, int? userId, int page, int pageSize)
    {
        await ExpireOldPendingAsync(null);
        var query = _db.Orders.AsNoTracking();
        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);
        return await PageAsync(query, status, page, pageSize, includeUserName: true);
    }

    // =====================================================================
    // DÀNH CHO PHẦN THANH TOÁN (BE4)
    // =====================================================================
    public async Task<bool> MarkPaidAsync(int orderId, DateTime? paidAtUtc = null)
    {
        var now = DateTime.UtcNow;
        // Chỉ đổi khi CHƯA Paid → callback gửi lặp nhiều lần cũng chỉ ghi nhận 1 lần.
        // Đơn đã Expired/Cancelled nhưng tiền vẫn về thành công → vẫn ghi nhận Paid để không mất tiền của người mua.
        var changed = await _db.Orders
            .Where(o => o.OrderId == orderId && o.Status != Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, Paid)
                .SetProperty(o => o.PaidAt, paidAtUtc ?? now)
                .SetProperty(o => o.UpdatedAt, now));
        return changed > 0;
    }

    public async Task<bool> MarkFailedAsync(int orderId)
    {
        var now = DateTime.UtcNow;
        var changed = await _db.Orders
            .Where(o => o.OrderId == orderId && o.Status == Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, Failed)
                .SetProperty(o => o.UpdatedAt, now));
        return changed > 0;
    }

    // =====================================================================
    // HÀM PHỤ
    // =====================================================================

    // Đơn Pending quá 30 phút → Expired (userId = null: tất cả người dùng)
    private async Task ExpireOldPendingAsync(int? userId)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - PendingLifetime;
        var query = _db.Orders.Where(o => o.Status == Pending && o.CreatedAt < cutoff);
        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);
        await query.ExecuteUpdateAsync(s => s
            .SetProperty(o => o.Status, Expired)
            .SetProperty(o => o.UpdatedAt, now));
    }

    // Đã mua truyện này chưa (có đơn Paid)
    private async Task<bool> OwnsStoryAsync(int userId, int storyId)
        => await _db.StoryPurchases.AnyAsync(p => p.Order.UserId == userId && p.Order.StoryId == storyId && p.RevokedAt == null && (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
           || await _db.Orders.AnyAsync(o => o.UserId == userId && o.OrderType == TypeStory
                                 && o.StoryId == storyId && o.Status == Paid);

    // Đơn đang chờ thanh toán cho đúng món hàng (nếu có)
    private Task<int?> FindPendingAsync(int userId, string type, int? storyId = null, int? planVersionId = null)
        => _db.Orders
            .Where(o => o.UserId == userId && o.Status == Pending && o.OrderType == type
                     && (storyId == null || o.StoryId == storyId)
                     && (planVersionId == null || o.PlanVersionId == planVersionId))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => (int?)o.OrderId)
            .FirstOrDefaultAsync();

    // Phiên bản gói đang bán = phiên bản ĐÃ CÔNG BỐ mới nhất (số phiên bản lớn nhất)
    private Task<ReadingPlanVersion?> CurrentVersionAsync(int planId)
    {
        var now = DateTime.UtcNow;
        return _db.ReadingPlanVersions.AsNoTracking()
            .Where(v => v.PlanId == planId && v.PublishedAt != null && v.PublishedAt <= now)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();
    }

    private static string PlanItemName(string planName, int months)
        => $"{planName} ({months} tháng)";

    private async Task<PagedResultDto<OrderDto>> PageAsync(
        IQueryable<Order> query, string? status, int page, int pageSize, bool includeUserName)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        var total = await query.CountAsync();
        var items = await ToDtos(query
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.OrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize), includeUserName)
            .ToListAsync();

        return new PagedResultDto<OrderDto>
        {
            Items = items.Select(Finish).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static IQueryable<OrderDto> ToDtos(IQueryable<Order> query, bool includeUserName)
        => query.Select(o => new OrderDto
        {
            OrderId = o.OrderId,
            UserId = o.UserId,
            UserName = includeUserName ? o.User.DisplayName : null,
            OrderType = o.OrderType,
            PlanId = o.PlanVersion != null ? o.PlanVersion.PlanId : null,
            PlanVersionId = o.PlanVersionId,
            StoryId = o.StoryId,
            ItemName = o.ItemName,
            Amount = o.Amount,
            DurationMonths = o.DurationMonths,
            Status = o.Status,
            CreatedAt = o.CreatedAt,
            PaidAt = o.PaidAt
        });

    // Tính các trường phụ: hạn thanh toán, còn thanh toán được không
    private static OrderDto Finish(OrderDto o)
    {
        if (o.Status == Pending)
        {
            o.ExpiresAt = o.CreatedAt + PendingLifetime;
            o.CanPay = o.ExpiresAt > DateTime.UtcNow;
        }
        return o;
    }
}

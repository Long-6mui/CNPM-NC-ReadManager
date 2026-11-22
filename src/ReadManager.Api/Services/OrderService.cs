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
    Task<OrderQuoteDto?> QuoteStoryAsync(int storyId, int? userId, int? voucherId = null);
    Task<OrderQuoteDto?> QuotePlanAsync(int planId, int? userId);

    // Tạo đơn. Trả null = không tìm thấy truyện / gói.
    // Ném ArgumentException (→ 400: dữ liệu sai / không bán) hoặc InvalidOperationException (→ 409: đã sở hữu / voucher không hợp lệ).
    Task<OrderDto?> CreateAsync(int userId, CreateOrderDto dto);

    // Hủy đơn Pending của chính người mua và giải phóng voucher nếu có.
    Task<OrderDto?> CancelAsync(int orderId, int userId);

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
    public async Task<OrderQuoteDto?> QuoteStoryAsync(int storyId, int? userId, int? voucherId = null)
    {
        var story = await _db.Stories.AsNoTracking()
            .Where(s => s.StoryId == storyId && s.Visibility == PublicVisibility)
            .Select(s => new { s.StoryId, s.Title, s.AccessPolicy, s.CurrentPrice })
            .FirstOrDefaultAsync();
        if (story is null)
            return null;

        var originalPrice = story.CurrentPrice ?? 0;
        var quote = new OrderQuoteDto
        {
            OrderType = TypeStory,
            StoryId = story.StoryId,
            ItemName = story.Title,
            Amount = originalPrice
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

            var pendingOrder = await _db.Orders.AsNoTracking()
                .Where(o => o.UserId == userId.Value && o.Status == Pending
                            && o.OrderType == TypeStory && o.StoryId == storyId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new { o.OrderId, o.Amount, o.OriginalAmount, o.DiscountAmount, o.VoucherId })
                .FirstOrDefaultAsync();

            quote.PendingOrderId = pendingOrder?.OrderId;

            if (voucherId.HasValue)
            {
                if (pendingOrder is not null && pendingOrder.VoucherId == voucherId.Value)
                {
                    quote.OriginalAmount = pendingOrder.OriginalAmount ?? originalPrice;
                    quote.DiscountAmount = pendingOrder.DiscountAmount;
                    quote.Amount = pendingOrder.Amount;
                    quote.VoucherId = pendingOrder.VoucherId;
                }
                else
                {
                    var now = DateTime.UtcNow;
                    var voucher = await _db.Vouchers.AsNoTracking()
                        .FirstOrDefaultAsync(v => v.VoucherId == voucherId.Value);
                    ValidateVoucherForStory(voucher, userId.Value, originalPrice, now);
                    var discount = CalculateVoucherDiscount(originalPrice, voucher!);
                    quote.OriginalAmount = originalPrice;
                    quote.DiscountAmount = discount;
                    quote.Amount = originalPrice - discount;
                    quote.VoucherId = voucher!.VoucherId;
                }
            }
            else if (pendingOrder is not null && pendingOrder.DiscountAmount > 0)
            {
                quote.OriginalAmount = pendingOrder.OriginalAmount ?? originalPrice;
                quote.DiscountAmount = pendingOrder.DiscountAmount;
                quote.Amount = pendingOrder.Amount;
                quote.VoucherId = pendingOrder.VoucherId;
            }
        }
        else if (voucherId.HasValue)
        {
            throw new ArgumentException("Vui lòng đăng nhập để áp dụng voucher.");
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
        if (dto.CouponId.HasValue && dto.VoucherId.HasValue)
            throw new ArgumentException("Không thể áp dụng đồng thời coupon gói đọc và voucher mua truyện.");
        if (dto.VoucherId.HasValue && dto.OrderType != TypeStory)
            throw new ArgumentException("Voucher chỉ áp dụng khi mua truyện.");
        if (dto.CouponId.HasValue && dto.OrderType != TypePlan)
            throw new ArgumentException("Mã giảm giá chỉ áp dụng khi mua gói đọc.");

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

        await using var tx = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync()
            : null;
        await _db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE UserId = {userId} FOR UPDATE")
            .AsNoTracking().ToListAsync();

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

        var now = DateTime.UtcNow;

        // 3. Đang có đơn chờ thanh toán cho đúng món này → dùng lại đơn đó (bấm "Mua" 2 lần không tạo 2 đơn)
        if (quote.PendingOrderId.HasValue)
        {
            var pendingInfo = await _db.Orders
                .Where(o => o.OrderId == quote.PendingOrderId.Value)
                .Select(o => new { o.CouponId, o.VoucherId })
                .FirstOrDefaultAsync();

            var switchingCoupon = dto.OrderType == TypePlan && dto.CouponId.HasValue && pendingInfo?.CouponId != dto.CouponId;
            var switchingVoucher = dto.OrderType == TypeStory && dto.VoucherId.HasValue && pendingInfo?.VoucherId != dto.VoucherId;

            if (!switchingCoupon && !switchingVoucher)
            {
                if (tx is not null) await tx.CommitAsync();
                return await GetByIdAsync(quote.PendingOrderId.Value, userId, isAdmin: false);
            }

            await _db.Orders.Where(o => o.OrderId == quote.PendingOrderId.Value && o.Status == Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, Cancelled).SetProperty(o => o.UpdatedAt, now));

            if (pendingInfo?.VoucherId is int oldVoucherId)
                await VoucherService.ReleaseUnusedVouchersAsync(_db, [oldVoucherId], now);
        }

        // Báo giá gốc trước giảm (nếu pending cũ có giảm giá thì lấy lại giá gốc của truyện/gói)
        var baseAmount = quote.OriginalAmount ?? quote.Amount;
        var finalAmount = baseAmount;
        decimal? originalAmount = null;
        var discount = 0m;
        int? couponId = null;
        int? voucherId = null;
        string? voucherName = null;

        // Sprint 5 — áp mã giảm giá từ chuỗi đọc (gói đọc)
        if (dto.CouponId.HasValue)
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.CouponId == dto.CouponId.Value && c.UserId == userId);
            if (coupon is null)
                throw new ArgumentException("Mã giảm giá không tồn tại.");
            if (coupon.ExpiresAt <= now)
                throw new ArgumentException("Mã giảm giá đã hết hạn.");
            var months = coupon.DurationMonthsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
            if (!months.Contains(quote.DurationMonths ?? 0))
                throw new ArgumentException($"Mã này chỉ áp dụng cho gói {string.Join(" / ", months)} tháng.");
            var inUse = await _db.Orders.AnyAsync(o => o.CouponId == coupon.CouponId && (o.Status == Pending || o.Status == Paid));
            if (inUse)
                throw new ArgumentException("Mã giảm giá đã được sử dụng.");

            originalAmount = baseAmount;
            discount = Math.Round(baseAmount * coupon.Percent / 100m, 0);
            finalAmount = baseAmount - discount;
            couponId = coupon.CouponId;
        }

        // Sprint 5 — áp voucher mua truyện và giữ chỗ (Reserved) cho đơn chờ thanh toán
        if (dto.VoucherId.HasValue)
        {
            var voucher = await _db.Vouchers
                .FromSqlInterpolated($"SELECT * FROM Vouchers WHERE VoucherId = {dto.VoucherId.Value} FOR UPDATE")
                .FirstOrDefaultAsync();
            ValidateVoucherForStory(voucher, userId, baseAmount, now);

            originalAmount = baseAmount;
            discount = CalculateVoucherDiscount(baseAmount, voucher!);
            finalAmount = baseAmount - discount;
            voucherId = voucher!.VoucherId;
            voucherName = voucher.Name;
            voucher.Status = VoucherStatuses.Reserved;
        }

        // 4. Tạo đơn mới, chụp lại giá + tên + số tháng
        var order = new Order
        {
            UserId = userId,
            OrderType = quote.OrderType,
            StoryId = quote.StoryId,
            PlanVersionId = quote.PlanVersionId,
            ItemName = quote.ItemName,
            Amount = finalAmount,
            OriginalAmount = originalAmount,
            DiscountAmount = discount,
            CouponId = couponId,
            VoucherId = voucherId,
            VoucherName = voucherName,
            DurationMonths = quote.DurationMonths,
            Status = Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        if (tx is not null)
            await tx.CommitAsync();

        return await GetByIdAsync(order.OrderId, userId, isAdmin: false);
    }

    public async Task<OrderDto?> CancelAsync(int orderId, int userId)
    {
        await using var tx = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync()
            : null;
        await _db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE UserId = {userId} FOR UPDATE")
            .AsNoTracking().ToListAsync();

        var now = DateTime.UtcNow;
        await ExpireOldPendingAsync(userId);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);
        if (order is null)
            return null;

        if (order.Status == Pending)
        {
            order.Status = Cancelled;
            order.UpdatedAt = now;
            await _db.SaveChangesAsync();

            if (order.VoucherId.HasValue)
                await VoucherService.ReleaseUnusedVouchersAsync(_db, [order.VoucherId.Value], now);
        }
        else if (order.Status != Cancelled && order.Status != Expired)
        {
            throw new InvalidOperationException($"Đơn đang ở trạng thái {order.Status}, không thể hủy.");
        }

        if (tx is not null)
            await tx.CommitAsync();

        return await GetByIdAsync(orderId, userId, isAdmin: false);
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
        var usedAt = paidAtUtc ?? now;
        // Chỉ đổi khi CHƯA Paid → callback gửi lặp nhiều lần cũng chỉ ghi nhận 1 lần.
        // Đơn đã Expired/Cancelled nhưng tiền vẫn về thành công → vẫn ghi nhận Paid để không mất tiền của người mua.
        var changed = await _db.Orders
            .Where(o => o.OrderId == orderId && o.Status != Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, Paid)
                .SetProperty(o => o.PaidAt, usedAt)
                .SetProperty(o => o.UpdatedAt, now));

        if (changed > 0)
        {
            var voucherId = await _db.Orders.AsNoTracking()
                .Where(o => o.OrderId == orderId)
                .Select(o => o.VoucherId)
                .FirstOrDefaultAsync();
            if (voucherId.HasValue)
            {
                // [Po] Voucher → Used; hủy đơn chờ khác đang giữ cùng voucher (tiền về trễ)
                await VoucherService.MarkUsedAsync(_db, orderId, voucherId.Value, usedAt);
                await _db.SaveChangesAsync();
            }
        }
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

        if (changed > 0)
        {
            var voucherId = await _db.Orders.AsNoTracking()
                .Where(o => o.OrderId == orderId)
                .Select(o => o.VoucherId)
                .FirstOrDefaultAsync();
            if (voucherId.HasValue)
                await VoucherService.ReleaseUnusedVouchersAsync(_db, [voucherId.Value], now);
        }
        return changed > 0;
    }

    // =====================================================================
    // HÀM PHỤ
    // =====================================================================

    // Đơn Pending quá 30 phút → Expired + giải phóng voucher đang giữ (userId = null: tất cả người dùng)
    private Task ExpireOldPendingAsync(int? userId)
        => VoucherService.ExpirePendingOrdersAndVouchersAsync(_db, userId, DateTime.UtcNow);

    private static void ValidateVoucherForStory(Voucher? voucher, int userId, decimal originalPrice, DateTime now)
    {
        if (voucher is null || voucher.UserId != userId)
            throw new ArgumentException("Voucher không tồn tại hoặc không thuộc tài khoản của bạn.");
        if (voucher.Status == VoucherStatuses.Used)
            throw new InvalidOperationException("Voucher này đã được sử dụng.");
        if (voucher.Status == VoucherStatuses.Reserved)
            throw new InvalidOperationException("Voucher này đang được giữ cho một đơn chờ thanh toán khác.");
        if (voucher.Status == VoucherStatuses.Expired || voucher.ExpiresAt <= now)
            throw new InvalidOperationException("Voucher này đã hết hạn sử dụng.");
        if (voucher.Status != VoucherStatuses.Available)
            throw new InvalidOperationException("Voucher hiện không khả dụng.");
        if (voucher.MinOrderAmount is > 0m && originalPrice < voucher.MinOrderAmount.Value)
            throw new ArgumentException($"Đơn hàng tối thiểu {voucher.MinOrderAmount.Value:N0}₫ để áp dụng voucher này.");
    }

    private static decimal CalculateVoucherDiscount(decimal originalPrice, Voucher voucher)
    {
        decimal discount = voucher.DiscountType == VoucherDiscountTypes.Percent
            ? Math.Round(originalPrice * voucher.Value / 100m, 0, MidpointRounding.AwayFromZero)
            : voucher.Value;

        if (voucher.DiscountType == VoucherDiscountTypes.Percent && voucher.MaxDiscount is > 0m)
            discount = Math.Min(discount, voucher.MaxDiscount.Value);

        // Giữ tổng thanh toán > 0 để khớp ràng buộc DB (CK_Orders_Amount) và cổng thanh toán.
        var maxDiscount = originalPrice > 1000m
            ? originalPrice - 1000m
            : Math.Max(0m, originalPrice - 1m);

        return Math.Clamp(discount, 0m, maxDiscount);
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
            OriginalAmount = o.OriginalAmount,
            DiscountAmount = o.DiscountAmount,
            CouponId = o.CouponId,
            VoucherId = o.VoucherId,
            VoucherName = o.VoucherName ?? (o.Voucher != null ? o.Voucher.Name : null),
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

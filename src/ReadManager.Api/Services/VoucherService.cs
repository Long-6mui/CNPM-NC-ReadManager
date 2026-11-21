using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Vouchers;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

public interface IVoucherService
{
    Task<List<VoucherOfferDto>> GetOffersAsync();
    Task<VoucherDto?> RedeemAsync(int userId, int offerId, Guid requestId);
    Task<List<VoucherDto>> GetMyVouchersAsync(int userId, string? status = null);
}

public class VoucherService : IVoucherService
{
    private static readonly Dictionary<string, string> StatusLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        [VoucherStatuses.Available] = VoucherStatuses.Available,
        [VoucherStatuses.Reserved] = VoucherStatuses.Reserved,
        [VoucherStatuses.Used] = VoucherStatuses.Used,
        [VoucherStatuses.Expired] = VoucherStatuses.Expired
    };

    private readonly AppDbContext _db;

    public VoucherService(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<VoucherOfferDto>> GetOffersAsync()
        => _db.VoucherOffers.AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.OfferId)
            .Select(o => new VoucherOfferDto
            {
                OfferId = o.OfferId,
                Name = o.Name,
                Description = o.Description,
                CoinCost = o.CoinCost,
                DiscountType = o.DiscountType,
                Value = o.Value,
                MaxDiscount = o.MaxDiscount,
                MinOrderAmount = o.MinOrderAmount,
                ValidDays = o.ValidDays
            })
            .ToListAsync();

    public async Task<VoucherDto?> RedeemAsync(int userId, int offerId, Guid requestId)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId không hợp lệ.");

        var offer = await _db.VoucherOffers.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OfferId == offerId && o.IsActive);
        if (offer is null)
            return null;

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE UserId = {userId} FOR UPDATE")
            .AsNoTracking().ToListAsync();

        var existing = await _db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId && v.RequestId == requestId);
        if (existing is not null)
        {
            if (existing.OfferId != offerId)
                throw new InvalidOperationException("Mã yêu cầu đã được sử dụng cho voucher khác.");

            await tx.CommitAsync();
            return ToDto(existing);
        }

        var sourceKey = $"voucher:{requestId}";
        var added = await CoinLedger.AddAsync(
            _db,
            userId,
            -offer.CoinCost,
            sourceKey,
            "Voucher",
            $"Đổi voucher: {offer.Name}");

        if (!added)
        {
            var replay = await _db.Vouchers.AsNoTracking()
                .FirstOrDefaultAsync(v => v.UserId == userId && v.RequestId == requestId);
            if (replay is not null)
            {
                await tx.CommitAsync();
                return ToDto(replay);
            }
            throw new InvalidOperationException("Giao dịch đổi voucher bị trùng khóa.");
        }

        var now = DateTime.UtcNow;
        var voucher = new Voucher
        {
            UserId = userId,
            OfferId = offer.OfferId,
            RequestId = requestId,
            Name = offer.Name,
            DiscountType = offer.DiscountType,
            Value = offer.Value,
            MaxDiscount = offer.MaxDiscount,
            MinOrderAmount = offer.MinOrderAmount,
            CoinCost = offer.CoinCost,
            Status = VoucherStatuses.Available,
            CreatedAt = now,
            ExpiresAt = now.AddDays(offer.ValidDays)
        };

        _db.Vouchers.Add(voucher);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return ToDto(voucher);
    }

    public async Task<List<VoucherDto>> GetMyVouchersAsync(int userId, string? status = null)
    {
        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!StatusLookup.TryGetValue(status.Trim(), out normalizedStatus))
                throw new ArgumentException("Trạng thái voucher không hợp lệ.");
        }

        var now = DateTime.UtcNow;
        await ExpirePendingOrdersAndVouchersAsync(_db, userId, now);

        var query = _db.Vouchers.AsNoTracking().Where(v => v.UserId == userId);
        if (normalizedStatus is not null)
            query = query.Where(v => v.Status == normalizedStatus);

        return await query
            .OrderByDescending(v => v.VoucherId)
            .Select(v => new VoucherDto
            {
                VoucherId = v.VoucherId,
                OfferId = v.OfferId,
                Name = v.Name,
                DiscountType = v.DiscountType,
                Value = v.Value,
                MaxDiscount = v.MaxDiscount,
                MinOrderAmount = v.MinOrderAmount,
                CoinCost = v.CoinCost,
                Status = v.Status,
                CreatedAt = v.CreatedAt,
                ExpiresAt = v.ExpiresAt,
                UsedAt = v.UsedAt
            })
            .ToListAsync();
    }

    internal static async Task ExpirePendingOrdersAndVouchersAsync(AppDbContext db, int? userId, DateTime now)
    {
        var cutoff = now - OrderService.PendingLifetime;
        var expiredOrders = db.Orders.Where(o => o.Status == OrderService.Pending && o.CreatedAt < cutoff);
        if (userId.HasValue)
            expiredOrders = expiredOrders.Where(o => o.UserId == userId.Value);

        var voucherIds = await expiredOrders
            .Where(o => o.VoucherId != null)
            .Select(o => o.VoucherId!.Value)
            .Distinct()
            .ToListAsync();

        await expiredOrders.ExecuteUpdateAsync(s => s
            .SetProperty(o => o.Status, OrderService.Expired)
            .SetProperty(o => o.UpdatedAt, now));

        if (voucherIds.Count > 0)
            await ReleaseUnusedVouchersAsync(db, voucherIds, now);

        var availableVouchers = db.Vouchers.Where(v => v.Status == VoucherStatuses.Available && v.ExpiresAt <= now);
        if (userId.HasValue)
            availableVouchers = availableVouchers.Where(v => v.UserId == userId.Value);

        await availableVouchers.ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, VoucherStatuses.Expired));
    }

    internal static async Task ReleaseUnusedVouchersAsync(AppDbContext db, IReadOnlyCollection<int> voucherIds, DateTime now)
    {
        if (voucherIds.Count == 0) return;

        await db.Vouchers
            .Where(v => voucherIds.Contains(v.VoucherId)
                        && v.Status == VoucherStatuses.Reserved
                        && !db.Orders.Any(o => o.VoucherId == v.VoucherId
                                               && (o.Status == OrderService.Pending || o.Status == OrderService.Paid)))
            .ExecuteUpdateAsync(s => s.SetProperty(
                v => v.Status,
                v => v.ExpiresAt > now ? VoucherStatuses.Available : VoucherStatuses.Expired));
    }

    // Đơn orderId đã thanh toán thành công → voucher của đơn chuyển sang Used.
    //
    // [Po — Sprint 5] CHỐNG DÙNG 1 VOUCHER CHO 2 ĐƠN khi tiền về trễ:
    //   Đơn A giữ voucher V → A hết hạn 30 phút → V được trả lại (Available)
    //   → người dùng tạo đơn B, giữ lại V (Reserved) → tiền của A mới về (callback trễ) → A thành Paid.
    //   Lúc này V đã thuộc về A (tiền đã thu theo giá giảm), nên đơn B đang chờ cũng giữ V phải bị HỦY.
    //   B chưa trả tiền nên hủy không làm mất tiền của ai. Người dùng mua lại B với giá đúng.
    //
    // Trả về số đơn chờ bị hủy để PaymentService ghi log.
    internal static async Task<int> MarkUsedAsync(AppDbContext db, int orderId, int voucherId, DateTime usedAtUtc)
    {
        // Khóa dòng voucher để không chạy song song với lúc tạo đơn mới đang giữ voucher này
        // (dùng ToListAsync để EF không bọc câu FOR UPDATE vào subquery)
        var voucher = (await db.Vouchers
            .FromSqlInterpolated($"SELECT * FROM Vouchers WHERE VoucherId = {voucherId} FOR UPDATE")
            .ToListAsync()).FirstOrDefault();
        if (voucher is null)
            return 0;

        // Hủy các đơn CHỜ thanh toán KHÁC đang giữ cùng voucher (đơn Paid khác không đụng tới)
        var now = DateTime.UtcNow;
        var cancelled = await db.Orders
            .Where(o => o.VoucherId == voucherId && o.OrderId != orderId && o.Status == OrderService.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderService.Cancelled)
                .SetProperty(o => o.UpdatedAt, now));

        if (voucher.Status != VoucherStatuses.Used)
        {
            voucher.Status = VoucherStatuses.Used;
            voucher.UsedAt ??= usedAtUtc;
        }
        return cancelled;
    }

    private static VoucherDto ToDto(Voucher v) => new()
    {
        VoucherId = v.VoucherId,
        OfferId = v.OfferId,
        Name = v.Name,
        DiscountType = v.DiscountType,
        Value = v.Value,
        MaxDiscount = v.MaxDiscount,
        MinOrderAmount = v.MinOrderAmount,
        CoinCost = v.CoinCost,
        Status = v.Status,
        CreatedAt = v.CreatedAt,
        ExpiresAt = v.ExpiresAt,
        UsedAt = v.UsedAt
    };
}

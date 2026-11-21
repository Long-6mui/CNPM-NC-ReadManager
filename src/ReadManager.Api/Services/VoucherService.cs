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

    internal static async Task MarkUsedAsync(AppDbContext db, int voucherId, DateTime usedAtUtc)
    {
        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == voucherId);
        if (voucher is not null && voucher.Status != VoucherStatuses.Used)
        {
            voucher.Status = VoucherStatuses.Used;
            voucher.UsedAt ??= usedAtUtc;
        }
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

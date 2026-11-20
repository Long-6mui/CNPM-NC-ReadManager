using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

public static class CoinLedger
{
    public static Task<long> BalanceAsync(AppDbContext db, int userId) =>
        db.CoinTransactions.Where(t => t.UserId == userId).SumAsync(t => (long)t.Amount);

    // Call inside the business transaction after locking this user's row.
    // The caller commits the ledger entry together with the purchased item/reward.
    public static async Task<bool> AddAsync(AppDbContext db, int userId, int amount,
        string sourceKey, string kind, string description)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Coin changes require a transaction and user lock.");
        var existing = await db.CoinTransactions.SingleOrDefaultAsync(t => t.UserId == userId && t.SourceKey == sourceKey);
        if (existing is not null)
        {
            if (existing.Amount != amount || existing.Kind != kind)
                throw new InvalidOperationException("Conflicting coin transaction.");
            return false;
        }
        var balance = await BalanceAsync(db, userId);
        if (balance + amount < 0) throw new InvalidOperationException("Không đủ xu.");
        db.CoinTransactions.Add(new CoinTransaction { UserId = userId, Amount = amount,
            BalanceAfter = balance + amount, SourceKey = sourceKey, Kind = kind, Description = description });
        await db.SaveChangesAsync();
        return true;
    }
}

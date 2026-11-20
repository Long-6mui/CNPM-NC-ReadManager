using Microsoft.EntityFrameworkCore;
namespace ReadManager.Api.Entities;

[Index(nameof(UserId), nameof(SourceKey), IsUnique = true)]
[Index(nameof(UserId), nameof(CoinTransactionId))]
public class CoinTransaction
{
    public long CoinTransactionId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int Amount { get; set; }
    public long BalanceAfter { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string SourceKey { get; set; } = "";
    [System.ComponentModel.DataAnnotations.MaxLength(40)]
    public string Kind { get; set; } = "";
    [System.ComponentModel.DataAnnotations.MaxLength(250)]
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[PrimaryKey(nameof(UserId), nameof(ReadDate))]
public class QualifiedReadingDay
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly ReadDate { get; set; }
}

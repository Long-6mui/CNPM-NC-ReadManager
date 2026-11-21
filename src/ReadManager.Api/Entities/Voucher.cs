using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.Entities;

public static class VoucherDiscountTypes
{
    public const string Percent = "Percent";
    public const string Amount = "Amount";
}

public static class VoucherStatuses
{
    public const string Available = "Available";
    public const string Reserved = "Reserved";
    public const string Used = "Used";
    public const string Expired = "Expired";
}

public class VoucherOffer
{
    public int OfferId { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int CoinCost { get; set; }

    [MaxLength(20)]
    public string DiscountType { get; set; } = VoucherDiscountTypes.Percent;

    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int ValidDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Voucher
{
    public int VoucherId { get; set; }
    public int UserId { get; set; }
    public int OfferId { get; set; }
    public Guid RequestId { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string DiscountType { get; set; } = VoucherDiscountTypes.Percent;

    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int CoinCost { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = VoucherStatuses.Available;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
    public VoucherOffer Offer { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Vouchers;

public class VoucherOfferDto
{
    public int OfferId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CoinCost { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int ValidDays { get; set; }
}

public class RedeemVoucherDto
{
    [Required]
    public Guid RequestId { get; set; }
}

public class VoucherDto
{
    public int VoucherId { get; set; }
    public int OfferId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int CoinCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

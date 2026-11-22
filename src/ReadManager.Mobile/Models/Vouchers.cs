using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// GET api/vouchers/offers — gói voucher đổi bằng xu (VoucherOfferDto)
public class VoucherOffer
{
    public int OfferId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CoinCost { get; set; }
    public string DiscountType { get; set; } = "Percent";   // Percent | Amount
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int ValidDays { get; set; }

    public string DiscountText => VoucherText.Discount(DiscountType, Value);
    public string CostText => $"{CoinCost:N0} xu";
    public string ConditionText => VoucherText.Conditions(MaxDiscount, MinOrderAmount, $"Dùng trong {ValidDays} ngày");
}

// GET api/me/vouchers — voucher của tôi (VoucherDto)
public class MyVoucher
{
    public int VoucherId { get; set; }
    public int OfferId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percent";
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int CoinCost { get; set; }
    public string Status { get; set; } = "Available";   // Available | Reserved | Used | Expired
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public string DiscountText => VoucherText.Discount(DiscountType, Value);
    public string ConditionText => VoucherText.Conditions(MaxDiscount, MinOrderAmount, $"HSD {ExpiresAt.ToLocalTime():dd/MM/yyyy}");
    public string StatusText => Status switch
    {
        "Available" => "Có thể dùng",
        "Reserved" => "Đang giữ cho đơn",
        "Used" => "Đã dùng",
        "Expired" => "Hết hạn",
        _ => Status
    };
    public Color StatusColor => Status switch
    {
        "Available" => Color.FromArgb("#12A150"),
        "Reserved" => Color.FromArgb("#EAB308"),
        _ => Color.FromArgb("#A1A1AA")
    };
}

public static class VoucherText
{
    public static string Discount(string type, decimal value)
        => type == "Amount" ? $"Giảm {Fmt.Money(value)}" : $"Giảm {value:0.##}%";

    public static string Conditions(decimal? maxDiscount, decimal? minOrder, string tail)
    {
        var parts = new List<string>();
        if (minOrder is > 0) parts.Add($"Đơn từ {Fmt.Money(minOrder.Value)}");
        if (maxDiscount is > 0) parts.Add($"tối đa {Fmt.Money(maxDiscount.Value)}");
        parts.Add(tail);
        return string.Join(" · ", parts);
    }
}

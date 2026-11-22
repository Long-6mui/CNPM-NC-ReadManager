using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.Models;

// Một lựa chọn giảm giá ở màn Thanh toán:
//  - "Voucher": voucher đổi bằng xu  → chỉ dùng khi MUA TRỌN BỘ TRUYỆN (server tính số tiền giảm)
//  - "Coupon" : mã giảm từ CHUỖI ĐỌC → chỉ dùng khi MUA / GIA HẠN GÓI ĐỌC (giảm % theo số tháng của gói)
//  - "None"   : không dùng mã
public partial class DiscountOption : ObservableObject
{
    public string Kind { get; init; } = "None";
    public int Id { get; init; }
    public string Badge { get; init; } = string.Empty;    // "-10%", "-25K"
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool Eligible { get; init; } = true;
    public string Reason { get; init; } = string.Empty;   // lý do chưa dùng được (vd: chưa đủ giá trị tối thiểu)
    public int Percent { get; init; }                     // coupon chuỗi: % giảm

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stroke))]
    [NotifyPropertyChangedFor(nameof(Background))]
    [NotifyPropertyChangedFor(nameof(Check))]
    private bool isSelected;

    public bool IsNone => Kind == "None";
    public bool HasBadge => !IsNone;
    public bool HasReason => !string.IsNullOrEmpty(Reason);
    public double Opacity => Eligible ? 1 : 0.5;
    public string Check => IsSelected ? "✓" : string.Empty;
    public Color Stroke => IsSelected ? Color.FromArgb("#E11D48") : ThemeService.Get("WebBorder");
    public Color Background => IsSelected ? ThemeService.Get("WebPill") : ThemeService.Get("WebSurface");

    // Màu cuống vé: voucher đỏ hồng, mã chuỗi tím
    public Color StubColor => Kind == "Coupon" ? Color.FromArgb("#7C3AED") : Color.FromArgb("#E11D48");

    public static DiscountOption None() => new()
    {
        Kind = "None", Title = "Không dùng mã giảm giá", Detail = "Thanh toán giá gốc"
    };

    public static DiscountOption FromVoucher(MyVoucher v, decimal baseAmount)
    {
        var minOk = v.MinOrderAmount is not > 0 || baseAmount >= v.MinOrderAmount.Value;
        return new DiscountOption
        {
            Kind = "Voucher",
            Id = v.VoucherId,
            Badge = v.BadgeText,
            Title = $"{v.DiscountText} · {v.Name}",
            Detail = v.ConditionText,
            Eligible = v.IsUsable && minOk,
            Reason = !v.IsUsable ? v.StatusText
                   : !minOk ? $"Cần đơn từ {Fmt.Money(v.MinOrderAmount!.Value)}" : string.Empty
        };
    }

    public static DiscountOption FromCoupon(StreakCoupon c, int planMonths)
    {
        var fits = c.DurationMonths.Count == 0 || c.DurationMonths.Contains(planMonths);
        return new DiscountOption
        {
            Kind = "Coupon",
            Id = c.CouponId,
            Badge = c.BadgeText,
            Title = $"{c.Name} 🔥",
            Detail = $"{c.MonthsText} · HSD {c.ExpiresText}",
            Percent = c.Percent,
            Eligible = fits,
            Reason = fits ? string.Empty : "Không áp dụng cho gói này"
        };
    }
}

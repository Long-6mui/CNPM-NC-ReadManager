using System.Text.RegularExpressions;
using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// ===================== SPRINT 2: GÓI ĐỌC + ĐƠN HÀNG =====================
// Tên trường khớp DTO của API: ReadingPlanDto / PlanVersionDto / OrderQuoteDto / OrderDto / CreateOrderDto.

public static class Fmt
{
    // API trả giờ UTC (đôi khi không có hậu tố Z) -> đổi sang giờ máy
    public static DateTime Local(DateTime utc)
        => utc.Kind == DateTimeKind.Local ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    public static string Money(decimal amount) => $"{amount:N0}₫";
}

public class PlanVersion
{
    public int PlanVersionId { get; set; }
    public int VersionNumber { get; set; }
    public decimal Price { get; set; }
    public int DurationMonths { get; set; }
    public string ScopeType { get; set; } = "All";   // All | Selected
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<int> StoryIds { get; set; } = new();
}

public class ReadingPlan
{
    public int PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public PlanVersion? CurrentVersion { get; set; }

    // ----- Chỉ để hiển thị -----
    public bool CanBuy => IsAvailable && CurrentVersion is not null;
    public string PriceText => CurrentVersion is null ? "—" : Fmt.Money(CurrentVersion.Price);
    public string DurationText => CurrentVersion is null ? string.Empty : $"Thời hạn {CurrentVersion.DurationMonths} tháng";
    public string ScopeText => CurrentVersion is null ? string.Empty
        : CurrentVersion.ScopeType == "All"
            ? "Đọc tất cả truyện trả phí"
            : $"Áp dụng cho {CurrentVersion.StoryIds.Count} truyện được chọn";
    public int PlanVersionId => CurrentVersion?.PlanVersionId ?? 0;
}

// Báo giá trước khi mua (GET api/orders/quote/story/{id} | plan/{id})
public class OrderQuote
{
    public string OrderType { get; set; } = string.Empty;   // Plan | Story
    public int? StoryId { get; set; }
    public int? PlanId { get; set; }
    public int? PlanVersionId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? DurationMonths { get; set; }
    public bool CanBuy { get; set; }
    public bool AlreadyOwned { get; set; }
    public int? PendingOrderId { get; set; }
    public string? Message { get; set; }

    public string AmountText => Fmt.Money(Amount);
}

// Body của POST api/orders — chỉ gửi "mua cái gì", KHÔNG gửi giá (giá lấy từ database)
public class CreateOrderRequest
{
    public string OrderType { get; set; } = string.Empty;
    public int? StoryId { get; set; }
    public int? PlanId { get; set; }
    public int? PlanVersionId { get; set; }
}

public class Order
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public string OrderType { get; set; } = string.Empty;   // Plan | Story
    public int? PlanId { get; set; }
    public int? PlanVersionId { get; set; }
    public int? StoryId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? DurationMonths { get; set; }
    public string Status { get; set; } = string.Empty;      // Pending | Paid | Failed | Cancelled | Expired
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool CanPay { get; set; }

    // ----- Chỉ để hiển thị -----
    public string Code => $"Đơn #{OrderId}";
    public bool IsStory => OrderType == "Story";
    public bool IsPending => Status == "Pending";
    public bool IsPaid => Status == "Paid";
    public bool ShowPay => IsPending && CanPay;
    public bool HasStory => StoryId.HasValue;
    public string TypeLabel => IsStory ? "📖 Mua trọn bộ" : "🎟 Gói đọc";
    public string AmountText => Fmt.Money(Amount);
    public string DurationText => DurationMonths.HasValue ? $"{DurationMonths} tháng" : "Vĩnh viễn, tất cả chương";
    public string CreatedText => Fmt.Local(CreatedAt).ToString("HH:mm dd/MM/yyyy");
    public string PaidText => PaidAt.HasValue ? Fmt.Local(PaidAt.Value).ToString("HH:mm dd/MM/yyyy") : "—";
    public bool HasPaidAt => PaidAt.HasValue;
    public string ExpiresText => ExpiresAt.HasValue ? $"Hạn thanh toán: {Fmt.Local(ExpiresAt.Value):HH:mm dd/MM/yyyy}" : string.Empty;
    public string ListLine => $"{CreatedText} · {AmountText}";

    public string StatusLabel => Status switch
    {
        "Pending" => "Chờ thanh toán",
        "Paid" => "Đã thanh toán",
        "Failed" => "Thanh toán thất bại",
        "Cancelled" => "Đã hủy",
        "Expired" => "Hết hạn thanh toán",
        _ => Status
    };

    // Màu giống badge của web: vàng / xanh / đỏ / xám
    public Color StatusColor => Status switch
    {
        "Pending" => Color.FromArgb("#B7791F"),
        "Paid" => Color.FromArgb("#12A150"),
        "Failed" => Color.FromArgb("#E5484D"),
        _ => Color.FromArgb("#5B6B88")
    };
}

// ===================== THƯ VIỆN ĐÃ MUA (quyền thực tế do API cung cấp) =====================

public class PlanEntitlement
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }   // giờ máy

    public string ExpiresText => $"Còn hạn đến {ExpiresAt:dd/MM/yyyy}";
    public int DaysLeft => Math.Max(0, (int)Math.Ceiling((ExpiresAt - DateTime.Now).TotalDays));
    public bool IsExpiringSoon => DaysLeft <= 7;
    public string RemainingText
    {
        get
        {
            var days = (int)Math.Ceiling((ExpiresAt - DateTime.Now).TotalDays);
            return days <= 0 ? "Sắp hết hạn" : $"Còn {days} ngày";
        }
    }
}

public class OwnedStory
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime PurchasedAt { get; set; }   // giờ máy
    public decimal? Amount { get; set; }

    public string PurchasedText => $"Được cấp quyền ngày {PurchasedAt:dd/MM/yyyy}" + (Amount.HasValue ? $" · {Fmt.Money(Amount.Value)}" : "");
}

public class PurchaseSummary
{
    public List<PlanEntitlement> Plans { get; set; } = new();
    public List<OwnedStory> Stories { get; set; } = new();

    // "Gói đọc tháng (1 tháng)" -> "Gói đọc tháng"
    public static string PlanNameOf(string itemName)
        => Regex.Replace(itemName ?? string.Empty, @"\s*\(\d+\s*tháng\)\s*$", string.Empty);
}

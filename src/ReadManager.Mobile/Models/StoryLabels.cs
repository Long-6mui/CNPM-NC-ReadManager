using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// Nhãn hiển thị dùng chung — giữ đúng chữ và màu như web (Views/Shared/_StoryCard.cshtml)
public static class StoryLabels
{
    public static string Access(string? access) => access switch
    {
        "Free" => "Miễn phí",
        "Paid" => "Trả phí",
        _ => "Kết hợp"
    };

    public static Color AccessColor(string? access) => access switch
    {
        "Free" => Color.FromArgb("#12A150"),
        "Paid" => Color.FromArgb("#E5484D"),
        _ => Color.FromArgb("#173FB0")
    };

    public static string Status(string? status) => status switch
    {
        "Ongoing" => "Đang ra",
        "Completed" => "Hoàn thành",
        _ => "Tạm ngừng"
    };

    // Giống hàm Ago() ở Views/Home/Index.cshtml
    public static string Ago(DateTime t)
    {
        var utc = t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var s = DateTime.UtcNow - utc;
        if (s.TotalMinutes < 1) return "vừa xong";
        if (s.TotalHours < 1) return $"{(int)s.TotalMinutes} phút trước";
        if (s.TotalDays < 1) return $"{(int)s.TotalHours} giờ trước";
        if (s.TotalDays < 30) return $"{(int)s.TotalDays} ngày trước";
        return utc.ToLocalTime().ToString("dd/MM/yyyy");
    }
}

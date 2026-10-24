namespace ReadManager.Api.Entities;

// PB36 — CÁC TRẠNG THÁI CỦA TÀI KHOẢN (cột Users.AccountStatus)
//
//   Active     = bình thường
//   Restricted = BỊ HẠN CHẾ: vẫn đăng nhập và ĐỌC được, nhưng không được bình luận / báo cáo / theo dõi truyện
//   Suspended  = BỊ TẠM NGỪNG: không đăng nhập được, mọi phiên đang đăng nhập bị thu hồi
//   Disabled   = tài khoản hệ thống (vd: tài khoản nguồn khi import ZIP) — không hiện, không chỉnh sửa
public static class AccountStatuses
{
    public const string Active = "Active";
    public const string Restricted = "Restricted";
    public const string Suspended = "Suspended";
    public const string Disabled = "Disabled";

    // Các trạng thái Admin được phép đặt cho thành viên
    public static readonly string[] Assignable = { Active, Restricted, Suspended };

    // Đăng nhập / giữ phiên được không? (Hạn chế vẫn đăng nhập được)
    public static bool CanSignIn(string? status)
        => status == Active || status == Restricted;

    // Tương tác (bình luận, báo cáo, theo dõi...) được không?
    public static bool CanInteract(string? status)
        => status == Active;

    // Chuẩn hóa chữ hoa/thường ("restricted" → "Restricted"); sai → null
    public static string? Normalize(string? status)
        => Assignable.FirstOrDefault(s => string.Equals(s, status?.Trim(), StringComparison.OrdinalIgnoreCase));
}

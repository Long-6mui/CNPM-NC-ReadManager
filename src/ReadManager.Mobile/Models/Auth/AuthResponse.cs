using System.Text.Json.Serialization;

namespace ReadManager.Mobile.Models.Auth;

public class AuthResponse
{
    // API trả token ở trường "accessToken"
    [JsonPropertyName("accessToken")]
    public string Token { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public UserProfile User { get; set; } = new();
}

public class UserProfile
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Member";

    // ----- Trạng thái tài khoản (Backend 2 — UserRestrictionService) -----
    // Hiện /auth/me CHƯA trả các trường này (chỉ có userId, email, displayName, role) nên mặc định = bình thường.
    // Đặt tên theo DTO của Backend 2 (UserManagementDtos: accountStatus, statusReason); nếu sau này /auth/me trả thêm
    // thì khung cảnh báo ở Tài khoản tự hiện. Trong lúc chờ, app dựa vào lỗi 403 khi bình luận/báo cáo/theo dõi.
    [JsonPropertyName("accountStatus")]
    public string? Status { get; set; }              // Active | Restricted | Suspended
    [JsonPropertyName("statusReason")]
    public string? RestrictionReason { get; set; }
    public DateTime? RestrictedUntil { get; set; }   // API chưa có -> luôn null

    [JsonIgnore]
    public bool IsRestricted =>
        string.Equals(Status, "Restricted", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Suspended", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string StatusMessage
    {
        get
        {
            if (!IsRestricted) return string.Empty;
            var locked = string.Equals(Status, "Suspended", StringComparison.OrdinalIgnoreCase);
            var text = locked ? "Tài khoản của bạn đang bị tạm ngừng." : "Tài khoản của bạn đang bị hạn chế: vẫn đọc được, nhưng không thể bình luận, báo cáo hay theo dõi truyện.";
            if (RestrictedUntil.HasValue) text += $" Đến {ReadManager.Mobile.Models.Fmt.Local(RestrictedUntil.Value):dd/MM/yyyy HH:mm}.";
            if (!string.IsNullOrWhiteSpace(RestrictionReason)) text += $" Lý do: {RestrictionReason}";
            return text;
        }
    }
}

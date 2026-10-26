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

    // ----- Trạng thái tài khoản (Backend 2 — Users/restriction, CHƯA có trên main) -----
    // Tên trường là DỰ ĐOÁN. Nếu API không trả các trường này thì giữ mặc định = tài khoản bình thường.
    // Khi Backend 2 xong: sửa tên cho khớp (vd "status", "restrictedUntil", "restrictionReason").
    public string? Status { get; set; }              // Active | Restricted | Locked
    public string? RestrictionReason { get; set; }
    public DateTime? RestrictedUntil { get; set; }   // UTC

    [JsonIgnore]
    public bool IsRestricted =>
        string.Equals(Status, "Restricted", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Locked", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string StatusMessage
    {
        get
        {
            if (!IsRestricted) return string.Empty;
            var locked = string.Equals(Status, "Locked", StringComparison.OrdinalIgnoreCase);
            var text = locked ? "Tài khoản của bạn đang bị khóa." : "Tài khoản của bạn đang bị hạn chế bình luận/báo cáo.";
            if (RestrictedUntil.HasValue) text += $" Đến {ReadManager.Mobile.Models.Fmt.Local(RestrictedUntil.Value):dd/MM/yyyy HH:mm}.";
            if (!string.IsNullOrWhiteSpace(RestrictionReason)) text += $" Lý do: {RestrictionReason}";
            return text;
        }
    }
}

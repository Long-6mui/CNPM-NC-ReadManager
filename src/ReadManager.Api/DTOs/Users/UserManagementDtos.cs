using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Users;

// PB35 — 1 dòng trong danh sách thành viên (Admin)
// CỐ Ý KHÔNG có PasswordHash / SecurityVersion → không bao giờ lộ ra ngoài.
public class MemberListItemDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Thông tin sử dụng dịch vụ (tóm tắt)
    public int ChapterReadCount { get; set; }     // tổng số lượt đọc chương
    public DateTime? LastReadAt { get; set; }     // lần đọc gần nhất (null = chưa đọc)
}

// PB35 — Chi tiết 1 thành viên (Admin bấm vào 1 dòng)
public class MemberDetailDto : MemberListItemDto
{
    public string? AvatarUrl { get; set; }
    public DateTime UpdatedAt { get; set; }

    // PB36 — vì sao bị hạn chế / tạm ngừng, từ lúc nào
    public string? StatusReason { get; set; }
    public DateTime? StatusChangedAt { get; set; }

    // Thông tin sử dụng dịch vụ (đầy đủ)
    public int ReadingStoryCount { get; set; }    // số truyện đang đọc dở
    public int FollowCount { get; set; }          // số truyện đang theo dõi
    public int CommentCount { get; set; }         // số bình luận đã đăng
    public int PaidOrderCount { get; set; }       // số đơn đã thanh toán
    public decimal TotalPaid { get; set; }        // tổng tiền đã thanh toán
    public bool HasActivePlan { get; set; }       // đang có gói đọc còn hạn
    public DateTime? PlanEndsAt { get; set; }     // gói đọc hết hạn lúc nào (UTC)
}

// PB36 — DỮ LIỆU GỬI LÊN KHI ĐỔI TRẠNG THÁI TÀI KHOẢN
//   PUT api/admin/users/7/status   body: { "status": "Restricted", "reason": "Spam bình luận" }
public class ChangeUserStatusDto
{
    // "Active" | "Restricted" | "Suspended"
    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    public string Status { get; set; } = string.Empty;

    // Bắt buộc khi hạn chế / tạm ngừng (để còn truy lại, tránh khóa nhầm không rõ lý do)
    [StringLength(500, ErrorMessage = "Lý do tối đa 500 ký tự.")]
    public string? Reason { get; set; }
}

// PB36 — Kết quả sau khi đổi trạng thái
public class UserStatusResultDto
{
    public int UserId { get; set; }
    public string AccountStatus { get; set; } = string.Empty;
    public string? StatusReason { get; set; }
    public DateTime? StatusChangedAt { get; set; }
}

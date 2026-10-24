using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ReadManager.Api.Services;

namespace ReadManager.Api.Authentication;

// PB36 — CHẶN TÀI KHOẢN BỊ HẠN CHẾ KHỎI CÁC HÀNH ĐỘNG "ĐĂNG BÀI"
//
// Cách dùng: thay [Authorize] bằng [Authorize(Policy = AuthPolicies.ActiveMember)]
// ở các API bình luận / báo cáo / theo dõi → tài khoản Restricted nhận 403 Forbidden.
// Các API chỉ ĐỌC (đọc chương, tiến độ đọc...) giữ nguyên [Authorize] → người bị hạn chế vẫn đọc được.
public static class AuthPolicies
{
    public const string ActiveMember = "ActiveMember";
}

public class ActiveAccountRequirement : IAuthorizationRequirement
{
}

public class ActiveAccountHandler : AuthorizationHandler<ActiveAccountRequirement>
{
    private readonly IUserRestrictionService _restriction;

    public ActiveAccountHandler(IUserRestrictionService restriction)
    {
        _restriction = restriction;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ActiveAccountRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;   // chưa đăng nhập → không Succeed → 401

        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return;

        // Hỏi thẳng database mỗi lần → Admin đổi trạng thái là có hiệu lực NGAY, không phải chờ token hết hạn
        if (await _restriction.CanInteractAsync(userId))
            context.Succeed(requirement);             // không Succeed → 403
    }
}

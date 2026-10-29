namespace ReadManager.Mobile.Services;

// Trạng thái tạm của phiên đăng nhập.
// API (Backend 2) chặn tài khoản "Restricted" bình luận / báo cáo / theo dõi bằng HTTP 403 nhưng chưa trả trạng thái
// trong /auth/me, nên app ghi nhớ lần bị 403 để hiện khung cảnh báo ở Tài khoản. Xóa khi đăng nhập / đăng xuất.
public static class SessionFlags
{
    public static bool AccountRestricted { get; set; }
}

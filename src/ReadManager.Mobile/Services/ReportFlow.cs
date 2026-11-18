using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.Services;

// Luồng "Báo cáo" dùng chung cho truyện / chương / bình luận: kiểm tra đăng nhập -> chọn lý do -> (ghi chú nếu "Khác") -> gửi.
public class ReportFlow
{
    private readonly ICommunityService _community;
    private readonly IAuthService _auth;

    public ReportFlow(ICommunityService community, IAuthService auth)
    {
        _community = community;
        _auth = auth;
    }

    // targetType: "Story" | "Chapter" | "Comment"; what: chữ hiển thị, vd "truyện", "chương", "bình luận"
    public async Task RunAsync(string targetType, int targetId, string what)
    {
        var shell = Shell.Current;
        if (targetId <= 0) return;

        bool loggedIn;
        try { loggedIn = await _auth.IsLoggedInAsync(); }
        catch { loggedIn = _auth.CurrentUser is not null; }

        if (!loggedIn)
        {
            if (await shell.DisplayAlert("Cần đăng nhập", $"Đăng nhập để báo cáo {what}.", "Đăng nhập", "Để sau"))
                await shell.GoToAsync("//LoginPage");
            return;
        }

        try
        {
            var reasons = await _community.GetReportReasonsAsync();
            if (reasons.Count == 0)
            {
                await shell.DisplayAlert("Báo cáo", "Chưa tải được danh sách lý do. Vui lòng thử lại.", "Đóng");
                return;
            }

            var reason = await shell.DisplayActionSheet($"Báo cáo {what}", "Hủy", null, reasons.ToArray());
            if (string.IsNullOrEmpty(reason) || reason == "Hủy") return;

            string? note = null;
            if (reason == "Khác")
            {
                note = await shell.DisplayPromptAsync("Lý do khác", "Vui lòng ghi rõ lý do (tối đa 500 ký tự).",
                    "Gửi", "Hủy", maxLength: 500);
                if (note is null) return;
                if (string.IsNullOrWhiteSpace(note))
                {
                    await shell.DisplayAlert("Báo cáo", "Chọn lý do “Khác” thì cần ghi rõ lý do.", "Đóng");
                    return;
                }
            }

            await _community.SubmitReportAsync(new ReportRequest
            {
                TargetType = targetType,
                TargetId = targetId,
                Reason = reason,
                Note = note?.Trim()
            });
            await shell.DisplayAlert("Đã gửi báo cáo", "Cảm ơn bạn. Quản trị viên sẽ xem xét nội dung này.", "Đóng");
        }
        catch (ApiException ex)
        {
            // 409 = đã báo cáo rồi, 404 = nội dung không còn, 429 = gửi quá nhanh... -> hiện đúng lời của API
            await shell.DisplayAlert("Không gửi được báo cáo", ex.Message, "Đóng");
        }
        catch
        {
            await shell.DisplayAlert("Không gửi được báo cáo", "Không thể kết nối máy chủ. Vui lòng thử lại.", "Đóng");
        }
    }
}

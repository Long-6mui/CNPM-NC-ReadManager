using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

// Sprint 3 — API cộng đồng của Backend 3 (đã có trên nhánh main).
public interface ICommunityService
{
    // Theo dõi truyện
    Task<FollowStatus> GetFollowAsync(int storyId);
    Task<FollowStatus> SetFollowAsync(int storyId, bool follow);
    Task<List<FollowedStory>> GetMyFollowsAsync();

    // Bình luận (chapterId = null: lấy/đăng bình luận của truyện)
    Task<PagedResult<CommentItem>> GetCommentsAsync(int storyId, int? chapterId, int page, int pageSize);
    Task<CommentItem> AddCommentAsync(int storyId, int? chapterId, string content);
    Task DeleteCommentAsync(int commentId);

    // Thông báo
    Task<NotificationList> GetNotificationsAsync(int page, int pageSize);
    Task<int> GetUnreadCountAsync();
    Task MarkReadAsync(int notificationId);
    Task MarkAllReadAsync();

    // Báo cáo
    Task<List<string>> GetReportReasonsAsync();
    Task SubmitReportAsync(ReportRequest request);
}

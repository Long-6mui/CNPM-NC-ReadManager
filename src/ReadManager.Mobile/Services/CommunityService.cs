using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public class CommunityService : ICommunityService
{
    private readonly ApiClient _api;

    public CommunityService(ApiClient api) => _api = api;

    // ----- Theo dõi -----
    public async Task<FollowStatus> GetFollowAsync(int storyId)
        => await Guard(() => _api.GetAsync<FollowStatus>($"stories/{storyId}/follow")) ?? new FollowStatus { StoryId = storyId };

    public async Task<FollowStatus> SetFollowAsync(int storyId, bool follow)
    {
        if (follow)
            return await Guard(() => _api.PostAsync<object, FollowStatus>($"stories/{storyId}/follow", new { }))
                   ?? new FollowStatus { StoryId = storyId, IsFollowing = true };

        await Guard(async () => { await _api.DeleteAsync($"stories/{storyId}/follow"); return true; });
        return await GetFollowAsync(storyId);
    }

    public async Task<List<FollowedStory>> GetMyFollowsAsync()
        => await Guard(() => _api.GetAsync<List<FollowedStory>>("me/follows")) ?? new();

    // ----- Bình luận -----
    public async Task<PagedResult<CommentItem>> GetCommentsAsync(int storyId, int? chapterId, int page, int pageSize)
    {
        var uri = $"stories/{storyId}/comments?page={page}&pageSize={pageSize}";
        if (chapterId.HasValue) uri += $"&chapterId={chapterId.Value}";
        return await Guard(() => _api.GetAsync<PagedResult<CommentItem>>(uri)) ?? new PagedResult<CommentItem>();
    }

    public async Task<CommentItem> AddCommentAsync(int storyId, int? chapterId, string content)
    {
        var created = await Guard(() => _api.PostAsync<CreateCommentRequest, CommentItem>(
            $"stories/{storyId}/comments", new CreateCommentRequest { ChapterId = chapterId, Content = content }));
        return created ?? throw new ApiException(0, "Không nhận được phản hồi từ máy chủ.");
    }

    public async Task DeleteCommentAsync(int commentId)
        => await Guard(async () => { await _api.DeleteAsync($"comments/{commentId}"); return true; });

    // ----- Thông báo -----
    public async Task<NotificationList> GetNotificationsAsync(int page, int pageSize)
        => await Guard(() => _api.GetAsync<NotificationList>($"notifications?page={page}&pageSize={pageSize}"))
           ?? new NotificationList();

    public async Task<int> GetUnreadCountAsync()
        => await Guard(() => _api.GetAsync<int?>("notifications/unread-count")) ?? 0;

    public async Task MarkReadAsync(int notificationId)
        => await Guard(async () => { await _api.PutAsync<object, object>($"notifications/{notificationId}/read", new { }); return true; });

    public async Task MarkAllReadAsync()
        => await Guard(async () => { await _api.PutAsync<object, object>("notifications/read-all", new { }); return true; });

    // ----- Báo cáo -----
    public async Task<List<string>> GetReportReasonsAsync()
        => await Guard(() => _api.GetAsync<List<string>>("reports/reasons")) ?? new();

    public async Task SubmitReportAsync(ReportRequest request)
        => await Guard(async () => { await _api.PostAsync("reports", request); return true; });

    // 401/403 của API cộng đồng thường không kèm nội dung -> đổi thành câu dễ hiểu
    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiException ex) when (ex.StatusCode == 403 && ex.Message.Contains("HTTP 403"))
        {
            throw new ApiException(403, "Tài khoản của bạn đang bị hạn chế hoặc khóa nên không thực hiện được thao tác này.", ex.Error);
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            throw new ApiException(401, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", ex.Error);
        }
    }
}

namespace ReadManager.Web.Services;

// DỮ LIỆU MẪU (trong bộ nhớ): đơn hàng + quyền đọc.
// Gói đọc đã lấy từ API thật (PlansApiClient). API đơn hàng/thanh toán nằm ở nhánh feature/s2-orders
// chưa merge vào main, nên phần mua/thanh toán tạm giả lập ở đây. Khi API đơn hàng merge, thay các
// hàm bên dưới bằng lời gọi API — giữ nguyên tên hàm thì Controller/View không phải sửa.
public class OrderItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string OrderType { get; set; } = "Plan";      // Plan | Story
    public int PlanId { get; set; }
    public int PlanVersionId { get; set; }               // phiên bản đã chốt giá khi tạo đơn
    public int StoryId { get; set; }
    public string ItemName { get; set; } = "";
    public decimal Price { get; set; }                   // giá chốt tại thời điểm giao dịch
    public int Months { get; set; }
    public string ScopeType { get; set; } = "All";
    public List<int> ScopeStoryIds { get; set; } = [];
    public string Status { get; set; } = "Pending";      // Pending | Paid | Failed
    public bool GrantFailed { get; set; }                // Paid nhưng cập nhật quyền bị lỗi (PB25)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}

public class PlanEntitlement
{
    public string UserId { get; set; } = "";
    public int PlanId { get; set; }
    public string PlanName { get; set; } = "";
    public string ScopeType { get; set; } = "All";
    public List<int> StoryIds { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
}

public class StoryEntitlement
{
    public string UserId { get; set; } = "";
    public int StoryId { get; set; }
    public string Title { get; set; } = "";
    public DateTime PurchasedAt { get; set; }
}

public class DemoPlanStore
{
    private readonly object _lock = new();
    private readonly List<OrderItem> _orders = [];
    private readonly List<PlanEntitlement> _plans = [];
    private readonly List<StoryEntitlement> _stories = [];
    private int _nextOrder = 1;

    // ---------- Tạo đơn (giá do Controller lấy từ API, không nhận từ client) ----------
    public OrderItem CreatePlanOrder(string uid, string userName, ReadingPlanApi plan)
    {
        var v = plan.CurrentVersion!;
        lock (_lock)
        {
            // Không tạo đơn trùng: còn đơn chờ thanh toán của đúng phiên bản này thì dùng lại
            var open = _orders.FirstOrDefault(o => o.UserId == uid && o.OrderType == "Plan"
                && o.PlanVersionId == v.PlanVersionId && o.Status == "Pending");
            if (open is not null) return open;
            var o = new OrderItem
            {
                Id = _nextOrder++, UserId = uid, UserName = userName, OrderType = "Plan",
                PlanId = plan.PlanId, PlanVersionId = v.PlanVersionId, ItemName = plan.Name,
                Price = v.Price, Months = v.DurationMonths, ScopeType = v.ScopeType, ScopeStoryIds = [.. v.StoryIds]
            };
            _orders.Add(o); return o;
        }
    }

    // Trả null nếu đã sở hữu (không thu tiền lại)
    public OrderItem? CreateStoryOrder(string uid, string userName, int storyId, string title, decimal price)
    {
        lock (_lock)
        {
            if (OwnsStoryNoLock(uid, storyId)) return null;
            var open = _orders.FirstOrDefault(o => o.UserId == uid && o.OrderType == "Story" && o.StoryId == storyId && o.Status == "Pending");
            if (open is not null) return open;
            var o = new OrderItem { Id = _nextOrder++, UserId = uid, UserName = userName, OrderType = "Story", StoryId = storyId, ItemName = title, Price = price };
            _orders.Add(o); return o;
        }
    }

    // ---------- Thanh toán (giả lập cổng thanh toán) ----------
    // outcome: "ok" | "fail" | "grantfail" (thanh toán xong nhưng lỗi cập nhật quyền → Admin khôi phục)
    // Chỉ xử lý đơn đang Pending → bấm/gửi lặp không cấp quyền hai lần.
    public bool Pay(string uid, int orderId, string outcome)
    {
        lock (_lock)
        {
            var o = _orders.FirstOrDefault(x => x.Id == orderId && x.UserId == uid);
            if (o is null || o.Status != "Pending") return false;
            if (outcome == "fail") { o.Status = "Failed"; return true; }
            o.Status = "Paid"; o.PaidAt = DateTime.UtcNow;
            if (outcome == "grantfail") o.GrantFailed = true; else GrantNoLock(o);
            return true;
        }
    }

    // PB25 — Admin khôi phục quyền: chỉ với đơn đã Paid mà chưa cấp quyền; không thu thêm tiền, không cấp trùng
    public bool Recover(int orderId)
    {
        lock (_lock)
        {
            var o = _orders.FirstOrDefault(x => x.Id == orderId);
            if (o is null || o.Status != "Paid" || !o.GrantFailed) return false;
            GrantNoLock(o); o.GrantFailed = false; return true;
        }
    }

    private void GrantNoLock(OrderItem o)
    {
        if (o.OrderType == "Story")
        {
            if (!OwnsStoryNoLock(o.UserId, o.StoryId))
                _stories.Add(new StoryEntitlement { UserId = o.UserId, StoryId = o.StoryId, Title = o.ItemName, PurchasedAt = DateTime.UtcNow });
            return;
        }
        // Gia hạn: còn hạn thì cộng dồn vào ngày hết hạn, hết hạn thì tính từ bây giờ
        var e = _plans.FirstOrDefault(x => x.UserId == o.UserId && x.PlanId == o.PlanId && x.ExpiresAt > DateTime.UtcNow);
        if (e is null)
            _plans.Add(new PlanEntitlement { UserId = o.UserId, PlanId = o.PlanId, PlanName = o.ItemName, ScopeType = o.ScopeType,
                StoryIds = [.. o.ScopeStoryIds], ExpiresAt = DateTime.UtcNow.AddMonths(o.Months) });
        else e.ExpiresAt = e.ExpiresAt.AddMonths(o.Months);
    }

    // ---------- Truy vấn ----------
    public List<OrderItem> Orders(string uid) { lock (_lock) return _orders.Where(o => o.UserId == uid).OrderByDescending(o => o.Id).ToList(); }
    public List<OrderItem> AllOrders(string? status = null)
    { lock (_lock) return _orders.Where(o => string.IsNullOrEmpty(status) || o.Status == status).OrderByDescending(o => o.Id).ToList(); }
    public List<PlanEntitlement> PlanEntitlements(string uid)
    { lock (_lock) return _plans.Where(e => e.UserId == uid && e.ExpiresAt > DateTime.UtcNow).OrderBy(e => e.ExpiresAt).ToList(); }
    public List<StoryEntitlement> OwnedStories(string uid)
    { lock (_lock) return _stories.Where(s => s.UserId == uid).OrderByDescending(s => s.PurchasedAt).ToList(); }

    public bool OwnsStory(string uid, int storyId) { lock (_lock) return OwnsStoryNoLock(uid, storyId); }
    private bool OwnsStoryNoLock(string uid, int storyId) => _stories.Any(s => s.UserId == uid && s.StoryId == storyId);

    // PB22/PB23 — có quyền đọc truyện này không? (mua riêng luôn còn; gói chỉ tính khi còn hạn và đúng phạm vi)
    public bool CanRead(string uid, int storyId)
    {
        lock (_lock)
            return OwnsStoryNoLock(uid, storyId)
                || _plans.Any(e => e.UserId == uid && e.ExpiresAt > DateTime.UtcNow
                                   && (e.ScopeType == "All" || e.StoryIds.Contains(storyId)));
    }

    public bool HasPendingStoryOrder(string uid, int storyId)
    { lock (_lock) return _orders.Any(o => o.UserId == uid && o.OrderType == "Story" && o.StoryId == storyId && o.Status == "Pending"); }
}

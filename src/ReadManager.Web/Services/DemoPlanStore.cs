namespace ReadManager.Web.Services;

// DỮ LIỆU MẪU: giả lập API gói đọc cho đến khi Backend (s2-*) merge.
// Tên trường bám theo bảng ReadingPlans / ReadingPlanVersions để sau này đổi sang gọi API dễ dàng.
public class PlanItem
{
    public int PlanId { get; set; }
    public int PlanVersionId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Name { get; set; } = "";
    public bool IsAvailable { get; set; } = true;
    public decimal Price { get; set; }
    public int DurationMonths { get; set; } = 1;
    public string ScopeType { get; set; } = "All";   // All | Selected
}
public class OrderItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string PlanName { get; set; } = "";
    public decimal Price { get; set; }
    public int Months { get; set; }
    public string Status { get; set; } = "Pending";  // Pending | Paid | Failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Entitlement
{
    public string UserId { get; set; } = "";
    public string PlanName { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class DemoPlanStore
{
    private readonly object _lock = new();
    private readonly List<PlanItem> _rows =
    [
        new() { PlanId = 1, PlanVersionId = 1, Name = "Gói đọc tháng", Price = 49000, DurationMonths = 1 },
        new() { PlanId = 2, PlanVersionId = 2, Name = "Gói đọc 6 tháng", Price = 249000, DurationMonths = 6 },
        new() { PlanId = 3, PlanVersionId = 3, Name = "Gói đọc năm", Price = 449000, DurationMonths = 12 },
    ];
    private readonly List<OrderItem> _orders = [];
    private readonly List<Entitlement> _ent = [];
    private int _nextVersion = 4, _nextPlan = 4, _nextOrder = 1;

    // Phiên bản mới nhất của mỗi gói
    public List<PlanItem> Latest(bool onlyAvailable = false)
    {
        lock (_lock)
            return _rows.GroupBy(r => r.PlanId).Select(g => g.OrderBy(r => r.VersionNumber).Last())
                .Where(p => !onlyAvailable || p.IsAvailable).OrderBy(p => p.PlanId).ToList();
    }

    // planId = 0: tạo gói mới. planId > 0: phát hành phiên bản giá mới (đơn cũ giữ giá cũ).
    public void Save(int planId, string name, decimal price, int months, string scope, bool available)
    {
        lock (_lock)
        {
            var last = _rows.Where(r => r.PlanId == planId).OrderBy(r => r.VersionNumber).LastOrDefault();
            if (last is null) planId = _nextPlan++;
            else foreach (var r in _rows.Where(r => r.PlanId == planId)) { r.Name = name; r.IsAvailable = available; }
            _rows.Add(new PlanItem { PlanId = planId, PlanVersionId = _nextVersion++, VersionNumber = (last?.VersionNumber ?? 0) + 1,
                Name = name, IsAvailable = available, Price = price, DurationMonths = months, ScopeType = scope });
        }
    }

    public OrderItem? Buy(string uid, int versionId)
    {
        lock (_lock)
        {
            var v = _rows.FirstOrDefault(r => r.PlanVersionId == versionId && r.IsAvailable);
            if (v is null) return null;
            var o = new OrderItem { Id = _nextOrder++, UserId = uid, PlanName = v.Name, Price = v.Price, Months = v.DurationMonths };
            _orders.Add(o); return o;
        }
    }

    // Giả lập kết quả thanh toán. Thành công thì cấp quyền; gia hạn khi còn hạn sẽ cộng dồn.
    public bool Pay(string uid, int orderId, bool ok)
    {
        lock (_lock)
        {
            var o = _orders.FirstOrDefault(x => x.Id == orderId && x.UserId == uid);
            if (o is null || o.Status != "Pending") return false;
            o.Status = ok ? "Paid" : "Failed";
            if (ok)
            {
                var e = _ent.FirstOrDefault(x => x.UserId == uid && x.PlanName == o.PlanName && x.ExpiresAt > DateTime.UtcNow);
                if (e is null) _ent.Add(new Entitlement { UserId = uid, PlanName = o.PlanName, ExpiresAt = DateTime.UtcNow.AddMonths(o.Months) });
                else e.ExpiresAt = e.ExpiresAt.AddMonths(o.Months);
            }
            return true;
        }
    }

    public List<OrderItem> Orders(string uid) { lock (_lock) return _orders.Where(o => o.UserId == uid).OrderByDescending(o => o.Id).ToList(); }
    public List<Entitlement> Entitlements(string uid) { lock (_lock) return _ent.Where(e => e.UserId == uid && e.ExpiresAt > DateTime.UtcNow).OrderBy(e => e.ExpiresAt).ToList(); }
}

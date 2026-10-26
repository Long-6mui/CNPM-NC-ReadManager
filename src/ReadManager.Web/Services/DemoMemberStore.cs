namespace ReadManager.Web.Services;

public enum MemberState { Active, Restricted, Suspended }

public class MemberItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "User";            // User | Admin
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsSample { get; set; }                     // true = thành viên mẫu để minh họa
    // Thông tin sử dụng của thành viên MẪU (thành viên thật được tính từ DemoPlanStore / DemoReadingProgressStore)
    public string? SamplePlan { get; set; }
    public int SampleOwned { get; set; }
    public int SampleReading { get; set; }
    public int SampleComments { get; set; }
}

public class MemberRestriction
{
    public MemberState Kind { get; set; }                  // Restricted (cấm bình luận) | Suspended (không đăng nhập được)
    public string Reason { get; set; } = "";
    public DateTime From { get; set; } = DateTime.UtcNow;
    public DateTime? Until { get; set; }                   // null = đến khi Admin khôi phục
    public string By { get; set; } = "";
    public bool Active => Until is null || Until > DateTime.UtcNow;
}

public class MemberLog
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Admin { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
}

// PB35 + PB36 — DỮ LIỆU MẪU (trong bộ nhớ). Backend 2 (UsersController, UserManagementService,
// UserRestrictionService) chưa có trên main nên danh sách, hạn chế và tạm ngừng tài khoản được giả lập ở đây.
// Tài khoản THẬT được thêm vào danh sách mỗi khi đăng nhập (Touch). Khi API có, thay thân các hàm bằng lời gọi API.
public class DemoMemberStore
{
    private readonly object _lock = new();
    private readonly List<MemberItem> _members = [];
    private readonly Dictionary<string, MemberRestriction> _active = [];
    private readonly Dictionary<string, List<MemberLog>> _logs = [];

    public DemoMemberStore()
    {
        var now = DateTime.UtcNow;
        MemberItem M(string id, string name, string mail, int daysAgo, string? plan, int owned, int reading, int cmts) => new()
        {
            Id = id, Name = name, Email = mail, IsSample = true, JoinedAt = now.AddDays(-daysAgo), LastLoginAt = now.AddDays(-daysAgo / 10.0),
            SamplePlan = plan, SampleOwned = owned, SampleReading = reading, SampleComments = cmts
        };
        _members.AddRange([
            M("sample-1", "Nguyễn Văn An", "an.nguyen@example.com", 120, "Gói tháng", 3, 5, 24),
            M("sample-2", "Trần Thị Bích", "bich.tran@example.com", 90, null, 1, 2, 6),
            M("sample-3", "Lê Hoàng Cường", "cuong.le@example.com", 75, "Gói năm", 8, 9, 41),
            M("sample-4", "Phạm Minh Dũng", "dung.pham@example.com", 60, null, 0, 1, 33),
            M("sample-5", "Võ Thu Hà", "ha.vo@example.com", 45, "Gói tháng", 2, 4, 3),
            M("sample-6", "Đặng Quốc Khánh", "khanh.dang@example.com", 30, null, 0, 0, 12),
            M("sample-7", "Bùi Ngọc Lan", "lan.bui@example.com", 15, "Gói tháng", 1, 3, 0),
            M("sample-8", "Hồ Gia Minh", "minh.ho@example.com", 5, null, 0, 1, 2),
        ]);
        // Hai trường hợp có sẵn để xem giao diện
        _active["sample-4"] = new MemberRestriction { Kind = MemberState.Restricted, Reason = "Bình luận spam nhiều lần", By = "Hệ thống mẫu", From = now.AddDays(-2), Until = now.AddDays(5) };
        _logs["sample-4"] = [new MemberLog { At = now.AddDays(-2), Admin = "Hệ thống mẫu", Action = "Hạn chế bình luận 7 ngày", Reason = "Bình luận spam nhiều lần" }];
        _active["sample-6"] = new MemberRestriction { Kind = MemberState.Suspended, Reason = "Nhiều báo cáo vi phạm đã xác nhận", By = "Hệ thống mẫu", From = now.AddDays(-1) };
        _logs["sample-6"] = [new MemberLog { At = now.AddDays(-1), Admin = "Hệ thống mẫu", Action = "Tạm ngừng tài khoản", Reason = "Nhiều báo cáo vi phạm đã xác nhận" }];
    }

    // Gọi khi đăng nhập thành công: tài khoản thật xuất hiện trong danh sách Admin
    public void Touch(string id, string name, string email, string role)
    {
        lock (_lock)
        {
            var m = _members.FirstOrDefault(x => x.Id == id);
            if (m is null) { m = new MemberItem { Id = id, JoinedAt = DateTime.UtcNow }; _members.Add(m); }
            m.Name = name; m.Email = email; m.Role = role; m.LastLoginAt = DateTime.UtcNow;
        }
    }

    public MemberItem? Get(string id) { lock (_lock) return _members.FirstOrDefault(x => x.Id == id); }

    public (List<MemberItem> Items, int Total) Search(string? q, string? state, string? role, int page, int size)
    {
        lock (_lock)
        {
            IEnumerable<MemberItem> x = _members;
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                x = x.Where(m => m.Name.Contains(k, StringComparison.OrdinalIgnoreCase) || m.Email.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(role)) x = x.Where(m => m.Role == role);
            if (Enum.TryParse<MemberState>(state, out var st)) x = x.Where(m => StateOfNoLock(m.Id).Item1 == st);
            var all = x.OrderByDescending(m => m.JoinedAt).ToList();
            return (all.Skip((page - 1) * size).Take(size).ToList(), all.Count);
        }
    }

    // Trạng thái hiện tại; hạn chế đã hết hạn thì coi như Active
    public (MemberState State, MemberRestriction? Info) StateOf(string id) { lock (_lock) return StateOfNoLock(id); }
    private (MemberState, MemberRestriction?) StateOfNoLock(string id)
        => _active.TryGetValue(id, out var r) && r.Active ? (r.Kind, r) : (MemberState.Active, null);

    public List<MemberLog> History(string id)
    { lock (_lock) return _logs.TryGetValue(id, out var l) ? l.OrderByDescending(x => x.At).ToList() : []; }

    // PB36 — hạn chế bình luận / tạm ngừng. days = null: đến khi khôi phục. Trả về thông báo lỗi hoặc null nếu thành công.
    public string? Apply(string id, string adminId, string adminName, MemberState kind, string? reason, int? days)
    {
        lock (_lock)
        {
            var m = _members.FirstOrDefault(x => x.Id == id);
            if (m is null) return "Không tìm thấy thành viên.";
            if (id == adminId) return "Bạn không thể tự hạn chế tài khoản của mình.";
            if (m.Role == "Admin") return "Không thể hạn chế tài khoản quản trị viên.";
            if (kind == MemberState.Active) return "Hình thức hạn chế không hợp lệ.";
            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 5) return "Vui lòng nhập lý do (ít nhất 5 ký tự).";
            if (reason.Length > 500) return "Lý do tối đa 500 ký tự.";
            if (days is < 1 or > 365) return "Thời hạn từ 1 đến 365 ngày (hoặc để trống nếu không giới hạn).";

            _active[id] = new MemberRestriction { Kind = kind, Reason = reason, By = adminName, Until = days is null ? null : DateTime.UtcNow.AddDays(days.Value) };
            Log(id, adminName, (kind == MemberState.Suspended ? "Tạm ngừng tài khoản" : "Hạn chế bình luận") + (days is null ? " (không giới hạn)" : $" {days} ngày"), reason);
            return null;
        }
    }

    public string? Restore(string id, string adminName)
    {
        lock (_lock)
        {
            if (!_members.Any(x => x.Id == id)) return "Không tìm thấy thành viên.";
            if (!_active.Remove(id)) return "Tài khoản này không bị hạn chế.";
            Log(id, adminName, "Khôi phục tài khoản", "");
            return null;
        }
    }

    private void Log(string id, string admin, string action, string reason)
    {
        if (!_logs.TryGetValue(id, out var l)) _logs[id] = l = [];
        l.Add(new MemberLog { Admin = admin, Action = action, Reason = reason });
    }
}

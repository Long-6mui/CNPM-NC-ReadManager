# TỔNG KẾT HOÀN THÀNH BACKEND 3 (SPRINT 2 & SPRINT 3)

## 1. Nối lại Quyền đọc & Thư viện (PB22, PB23, PB27)

Đã chuyển đổi toàn bộ logic sang sử dụng các bảng chuẩn có sẵn trên nhánh `main` của nhóm (`UserSubscription`, `StoryPurchase`, `Order`), không tạo bảng trùng.

### EntitlementService (`IEntitlementService`)
- **Thứ tự kiểm tra quyền đọc chương / truyện**:
  1. Truyện hoặc chương `Free` $\rightarrow$ cho phép đọc (`AccessType: Free`).
  2. Người dùng là Admin $\rightarrow$ luôn được đọc (`AccessType: Admin`).
  3. Chưa đăng nhập $\rightarrow$ khóa, trả về `LockReason: LoginRequired`.
  4. Đã mua lẻ truyện (`StoryPurchases` còn hiệu lực) $\rightarrow$ cho phép đọc vĩnh viễn, kể cả khi không có gói hoặc gói đã hết hạn (**PB23**).
  5. Có gói đăng ký (`UserSubscriptions`) còn hiệu lực (`StartsAt <= now <= EndsAt` và chưa bị revoke):
     - Gói phạm vi toàn bộ truyện (`ScopeType == "All"`) $\rightarrow$ cho phép đọc.
     - Gói theo danh sách truyện (`ScopeType == "Selected"` và truyện nằm trong `PlanVersionStories`) $\rightarrow$ cho phép đọc.
  6. Nếu từng có gói bao phủ truyện này nhưng đã hết hạn $\rightarrow$ khóa, trả về `LockReason: SubscriptionExpired`.
  7. Chưa từng mua $\rightarrow$ khóa, trả về `StoryPurchaseRequired` hoặc `SubscriptionRequired`.

- **Cấp quyền (hỗ trợ Payment Webhook / Fulfillment gọi, an toàn lặp)**:
  - `GrantSubscriptionAsync(orderId)`: kiểm tra đơn hàng, tự động gia hạn nối tiếp `EndsAt` cũ nếu gói hiện tại còn hạn.
  - `GrantStoryPurchaseAsync(orderId)`: cấp quyền sở hữu truyện vĩnh viễn.

### Tích hợp vào ChapterService & ChapterReadDto
- `ChapterService` nhận `IEntitlementService` qua Dependency Injection (tham số tùy chọn, không làm hỏng các bài test cũ).
- Khi đọc chương (`GetByIdAsync`, `GetByNumberAsync`), nếu chương trả phí và người dùng có quyền thì tự động trả nội dung chương; nếu chưa có quyền thì ẩn nội dung và gán `LockReason`.
- Tự động ghi nhận `ChapterRead` khi mở đọc thành công (chống trùng F5 trong 30 phút).

### API Quyền đọc & Thư viện
- `GET api/stories/{id}/access`: kiểm tra quyền đọc truyện (khách vãng lai cũng gọi được).
- `GET api/chapters/{id}/access`: kiểm tra quyền đọc chương.
- `GET api/me/subscriptions`: danh sách các gói đang dùng, số ngày còn lại và lịch sử.
- `GET api/me/library`: toàn bộ truyện mua lẻ và gói thuê bao của người dùng.
- `GET api/me/library/purchases`: danh sách truyện mua lẻ.

---

## 2. Thống kê nội dung PB40 (Content Statistics)
- Kết nối dữ liệu thực tế từ:
  - Lượt đọc: `ChapterReads` (được ghi tự động từ `ChapterService`).
  - Theo dõi: `StoryFollows`.
  - Doanh thu / mua truyện: `Orders` (trạng thái `Paid`).
  - Tương tác: `Comments` (trạng thái `Visible`).
- Hỗ trợ các API thống kê dành cho Admin:
  - `GET api/admin/statistics/content/overview`: tổng quan lượt đọc, độc giả duy nhất, top truyện & thể loại.
  - `GET api/admin/statistics/content/stories`: bảng xếp hạng truyện theo lượt đọc/theo dõi/mua.
  - `GET api/admin/statistics/content/stories/{id}/chapters`: chi tiết từng chương của 1 truyện.
  - `GET api/admin/statistics/content/genres`: thống kê theo thể loại.
  - `GET api/admin/statistics/content/daily`: dữ liệu từng ngày theo múi giờ Việt Nam (vẽ biểu đồ).

---

## 3. Khám phá & Bảng xếp hạng (Discovery & Ranking & Trending)

### API Bảng xếp hạng truyện (`RankingController`)
- `GET api/ranking/stories?by={reads|follows|score}&period={day|week|month|all}&top={10}&genreId={id}`
  - Xếp hạng truyện theo lượt đọc thật (`ChapterReads`), lượt theo dõi (`StoryFollows`), hoặc điểm tổng hợp (`reads + follows * 5`).
  - Hỗ trợ xem theo ngày (24h), tuần (7 ngày), tháng (30 ngày), hoặc toàn thời gian.
  - Lọc theo thể loại nếu cần.

### API Xu hướng & Gợi ý (`DiscoveryController`)
- `GET api/discovery/trending`:
  - Trả về danh sách từ khóa tìm kiếm nổi bật được tổng hợp từ các truyện hot và tác giả hot nhất trong tuần.
  - Danh sách thể loại hot.
  - Top truyện nổi bật (`FeaturedStories`).
- `GET api/discovery/recommended`:
  - Gợi ý truyện cá nhân hóa cho người dùng dựa trên thể loại các truyện họ đã đọc gần đây trong `ChapterReads`.
  - Tự động fallback về top truyện thịnh hành nếu là khách vãng lai.

### Mở rộng Tìm kiếm truyện (`StoryService`)
- `GET api/stories?sort={reads|follows|updated|newest|title}`
  - Cho phép sắp xếp danh sách truyện trực tiếp theo số lượt đọc (`reads`) và số lượt theo dõi (`follows`).

# Tích hợp Web đơn mua gói

Nhánh codex/plan-orders-integration kế thừa nền DB của PR #25.

- Sửa input giá min=1/step=1: chấp nhận 50000.
- Plans/Buy gửi PlanId và phiên bản đang hiển thị sang POST api/orders; giá do API lấy từ DB.
- Mua truyện qua route cũ Plans/BuyStory cũng gọi API Orders.
- Plans/Mine lấy api/me/orders, Admin/Orders lấy api/admin/orders, có phân trang.
- Bỏ đăng ký DemoPlanStore và các action thanh toán/khôi phục giả khỏi controller đang dùng.
- Đơn Pending không cấp quyền. Dữ liệu mua thử cũ không được chuyển thành giao dịch đã xác minh.
- Lỗi tải bình luận không ngăn đọc nội dung chương; lỗi HTTP đọc chương hiển thị mã lỗi thay vì luôn báo mất kết nối.

Kiểm thử: ReadingPlanChecks 42 ca đạt trên MySQL tạm, gồm đơn giá 50000,
chống lặp tuần tự, lịch sử đúng tài khoản và không cấp quyền cho Pending.
WebOrderChecks 6 ca đạt với HTTP giả lập; Web/Razor build đạt.
Chương 1 truyện 2 ở API/Web đang chạy trả 200 và đọc được tại thời điểm kiểm tra;
chưa tái hiện lỗi đọc mà người dùng gặp khi đăng nhập.

Chưa làm: cổng thanh toán/callback xác minh, cấp quyền và kiểm tra quyền mua khi đọc.
Không coi việc tạo đơn là đã mua thành công. Chưa sửa DB chính, chưa restart phiên Web đang chạy.
Dừng Web trong VS rồi chạy lại để nạp code mới. Không cần chạy Add-Migration cho thay đổi Web này.

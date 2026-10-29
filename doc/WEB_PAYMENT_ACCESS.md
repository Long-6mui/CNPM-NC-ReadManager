# Kiểm thử Web thanh toán và quyền đọc

Nhánh: codex/web-payment-access. Nền main: 84132c9.

## Chạy trên máy

Mở hai terminal tại D:\Read-Management:

```powershell
dotnet run --project src/ReadManager.Api --launch-profile https
```

```powershell
dotnet run --project src/ReadManager.Web --launch-profile http
```

Mở http://localhost:5149. Web Api:BaseUrl phải trỏ tới API đang chạy.
Máy Long đã cấu hình Payment:SecretKey trong User Secrets, không lưu vào Git.
Máy khác cần cấu hình khóa riêng trước khi dùng mô phỏng thanh toán.

## Test thủ công

1. Đăng nhập Member, chọn một gói đang bán, tạo đơn.
2. Trong chi tiết đơn, bắt đầu thanh toán; kiểm tra số tiền.
3. Trong Development chọn thanh toán thử thành công. Đây là mô phỏng, không thu tiền thật.
4. Mở Gói và thanh toán của tôi: kiểm tra gói, thời hạn, trạng thái và đơn hàng.
5. Đọc chương trả phí thuộc gói: phải mở được. Gói Selected không được mở truyện ngoài phạm vi.
6. Tài khoản khác không được sử dụng đơn và quyền đọc của tài khoản vừa mua.
7. Thử đơn mới với kết quả thất bại hoặc hủy: không được cấp quyền đọc.
8. Admin > Đơn hàng > Giao dịch cần cấp lại quyền: lấy danh sách thật từ API; chỉ cấp lại khi thanh toán đã được xác minh. Danh sách trống là bình thường.

Mô phỏng chỉ hoạt động khi cả API và Web chạy Development. Production chưa có URL chuyển đến cổng thanh toán thật; không coi tạo yêu cầu là đã trả tiền.

## Kiểm tra tự động

```powershell
dotnet run --project tests/WebPaymentChecks
```

Tạo database thử ngẫu nhiên riêng bằng kết nối User Secrets, chạy API ở cổng tạm, kiểm tra 38 tình huống Web/Mobile qua client Web rồi xóa database thử. Tài khoản MySQL cần quyền tạo/xóa database thử. Không dùng dữ liệu người dùng để giả thanh toán.

## Mobile: đồng bộ route và DTO

Route là địa chỉ và phương thức HTTP. DTO là cấu trúc JSON gửi/nhận.
- Thanh toán: POST /api/payments/start gửi orderId; nhận paymentId, merchantRequestId, provider, orderId, amount, currency. API chưa trả checkoutUrl.
- Quyền đọc: GET /api/me/library trả purchasedStories và subscriptions; subscriptions là đối tượng có history, không phải mảng trực tiếp.
- Tiến độ: PUT /api/stories/{storyId}/progress gửi chapterId, scrollPercent; GET cùng địa chỉ lấy tiến độ; GET /api/me/reading lấy danh sách đang đọc.

Mobile đã cập nhật route/DTO và bật ba cờ API. Bản Debug có lựa chọn mô phỏng thanh toán (API phải chạy Development); Release chỉ tạo giao dịch và báo cổng thật chưa tích hợp. Tiến độ đọc lưu và khôi phục phần trăm cuộn, danh sách có phân trang, xóa tiến độ qua API. Quyền đọc dùng dữ liệu server, không suy ra từ đơn đã trả tiền.

## PB28–29: tiến độ Web–Mobile

Web đã dùng API chung cho nút Đọc tiếp, danh sách Đang đọc, lưu phần trăm cuộn và xóa tiến độ. Không cần migration mới.

1. Đăng nhập cùng tài khoản trên hai nền tảng.
2. Đọc/cuộn trên Mobile, rời màn đọc; mở trang chi tiết truyện trên Web và chọn Đọc tiếp.
3. Cuộn trên Web, chờ thông báo đã đồng bộ; mở lại Đọc tiếp trên Mobile.
4. Xóa truyện ở Đang đọc trên Web; tải lại thư viện Mobile: truyện không còn trong danh sách.
5. Đổi tài khoản: không thấy tiến độ tài khoản trước.
6. Khi API không chạy, trang đọc vẫn hiển thị nội dung nếu đã tải được, nhưng báo chưa đồng bộ; không báo lưu thành công giả.

Bộ kiểm tra JavaScript: `node tests/WebPaymentChecks/reading-progress.test.cjs`.

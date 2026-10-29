# Đối chiếu Sprint 2–3 ngày 28/10/2026

Nguồn: doc/ProductBacklog-nhom05-03.xlsx (sheet Sprint backlog), origin/main 84132c9 và thay đổi nhánh codex/web-payment-access. Đây là đối chiếu code và kiểm tra có phạm vi, không phải xác nhận mọi màn hình đã nghiệm thu.

## Đã sửa trong nhánh này

- Web: nối bắt đầu thanh toán, mô phỏng Development, thư viện quyền đọc, Admin xem giao dịch cần cấp lại quyền và cấp lại qua API.
- Mobile: POST payments/start và DTO thực tế; mô phỏng chỉ có giao diện trong Debug, API vẫn chặn ngoài Development. Tạo giao dịch không đồng nghĩa đã thanh toán.
- Mobile: GET me/library, ánh xạ purchasedStories + subscriptions.history, EndsAt và GrantedAt; không hiển thị giá mua giả bằng 0 khi API không cung cấp số tiền.
- Mobile: chỉ hiển thị gói IsActive chưa thu hồi. Không quay về suy luận quyền từ đơn hàng khi API lỗi.
- Mobile: PUT/GET/DELETE stories/{storyId}/progress và GET me/reading có phân trang. Lưu phần trăm cuộn, khôi phục vị trí, debounce khi cuộn, lưu khi rời màn hình. Khách lưu trên máy; người đăng nhập dùng API. Lỗi đồng bộ có thông báo.
- Mobile: kết quả rỗng từ server được giữ rỗng, không hiện lại lịch sử cũ của máy; đổi tài khoản lấy dữ liệu theo token mới.
- Mobile: xóa ở tab Đang đọc gọi API; lỗi tải gói không bị nuốt im lặng.
- Sửa bộ Sprint3Checks đang tham chiếu ReadingPlan.CreatedBy không tồn tại.

## Kết quả kiểm tra

- Build Android Debug: đạt, còn các cảnh báo XAML binding và nhánh cấu hình hằng.
- WebPaymentChecks: 38 kiểm tra đạt qua API chạy thật và database thử riêng; liên kết trực tiếp source client/model Mobile, không dùng bản DTO chép lại.
- Bao gồm: đúng route/DTO, đơn đúng chủ, giá server, quyền Selected, tài khoản khác không được đọc, callback lặp, thiếu grant, cấp lại idempotent, tiến độ và phần trăm cuộn, hơn 50 truyện, xóa tiến độ, phiên hết hạn, đổi tài khoản.
- Sprint3Checks: 11 kiểm tra đạt (quyền đọc, thư viện, xếp hạng, xu hướng, gợi ý). Tên bộ test không có nghĩa bao phủ toàn bộ Sprint 3.
- Chưa thao tác UI trên emulator trong phiên này vì không có thiết bị đang kết nối. Cần kiểm tra cuộn/khôi phục vị trí thực tế trên Android.

## Sprint 2

| Task | Kết luận hiện tại |
|---|---|
| PB16 Giá truyện | Có code API và Web; cần nghiệm thu với giá/validation thực tế. |
| PB17 Tạo gói | Có API và Web quản lý. |
| PB18 Sửa/ngừng gói | Có API và Web, giữ phiên bản gói. |
| PB19 Đăng ký/thanh toán gói | Luồng mô phỏng đã nối Web/Mobile và kiểm tra; chưa có cổng thanh toán thật. |
| PB20 Gia hạn | Backend nối tiếp ngày hết hạn; có luồng mua lại gói. Cần kiểm tra sớm/muộn, nhiều đơn cùng lúc và phiên bản gói thay đổi. |
| PB21 Mua truyện riêng | Có tạo đơn, payment và cấp quyền; cần demo end-to-end trên cả hai giao diện. Cổng thật vẫn thiếu nếu yêu cầu thu tiền thật. |
| PB22 Quyền đọc trả phí | Có backend và client; đã kiểm tra phạm vi Selected, All và người không có quyền. |
| PB23 Gói hết hạn | Có kiểm tra thời hạn; cần nghiệm thu hết hạn/thu hồi, vẫn giữ quyền mua riêng. |
| PB24 Thanh toán chưa thành công | Có xử lý callback, giao diện không tự cấp quyền; cần nghiệm thu cả Pending/Failed/Cancelled và chữ ký sai. |
| PB25 Khôi phục quyền | Đã nối Web Admin, test thiếu grant và cấp lại không trùng đạt. |
| PB26 Lịch sử thanh toán | Có lịch sử đơn hàng Web/Mobile; cần chốt có yêu cầu hiển thị từng lần thử thanh toán hay chỉ trạng thái đơn. |
| PB27 Truyện/gói đã mua | Đã nối API thực tế Web/Mobile; cần nghiệm thu UI và gói hết hạn/thu hồi. |

Không đánh dấu Sprint 2 hoàn thành 100% chỉ dựa vào mô phỏng. Nếu đồ án chấp nhận thanh toán giả lập, có thể nghiệm thu luồng đó; nếu yêu cầu cổng thật, PB19–21 vẫn còn phần tích hợp nhà cung cấp.

## Sprint 3

| Task | Kết luận hiện tại |
|---|---|
| PB09 Hẹn giờ phát hành | Có lịch công bố và chặn chương chưa đến giờ; cần test trước/đúng/sau giờ và đổi lịch. |
| PB13 Tìm kiếm | Có API và giao diện; cần nghiệm thu tên/tác giả/thể loại và không lộ truyện ẩn. |
| PB14 Khám phá | Có code; bộ Sprint3Checks kiểm tra xếp hạng/xu hướng/gợi ý. |
| PB15 Lượt đọc | Có code ghi nhận; cần nghiệm thu chống đếm lặp và chương chưa có quyền. |
| PB28 Tiến độ đọc | Đã nối API chung Web/Mobile; Web đọc lại vị trí, lưu phần trăm cuộn qua cookie + antiforgery. Test tích hợp hai chiều đạt; còn nghiệm thu cuộn thực tế trên browser/emulator. |
| PB29 Danh sách đang đọc | Đã nối API Web/Mobile, lấy đủ các trang và xóa đồng bộ; test qua MVC Web và client Mobile đạt. |
| PB30 Theo dõi | Có API và client; cần nghiệm thu thêm/bỏ và đổi tài khoản. |
| PB31 Thông báo | Có API và client; cần nghiệm thu chương hẹn giờ, đổi lịch, chương ẩn và đánh dấu đã đọc. |
| PB32 Thảo luận | Có API và client; cần nghiệm thu phân quyền, tài khoản hạn chế và dữ liệu chương. |
| PB33 Báo cáo | Có API và client; cần nghiệm thu loại nội dung, lý do và trạng thái xử lý. |
| PB34 Kiểm duyệt | Web ReportsController đã gọi API; cần nghiệm thu ẩn/xử lý và quyền Admin. |
| PB35 Danh sách thành viên | API /api/admin/users có, nhưng Web Admin MembersController vẫn dùng DemoMemberStore: CHƯA nối dữ liệu thật. |
| PB36 Hạn chế/tạm ngừng | Backend có API đổi trạng thái, nhưng Web Admin vẫn thao tác DemoMemberStore: CHƯA cập nhật DB qua giao diện. |

## Việc nên làm tiếp

1. PB28–29 đã nối API và kiểm tra tự động hai chiều. Còn nghiệm thu UI: đọc/cuộn trên Mobile rồi mở tiếp trên Web và ngược lại.
2. Nối Web Admin PB35–36 vào Users API; bỏ dùng dữ liệu tạm trong quyết định hạn chế/tạm ngừng. Test phiên đang đăng nhập sau khi bị khóa.
3. Chạy checklist UI Android cho thay đổi PR này: mua gói → thanh toán thử → mở chương; đổi tài khoản; đọc/cuộn/đóng app/mở lại; xóa đang đọc; mất mạng/phiên hết hạn.
4. Chốt mô phỏng hay cổng thanh toán thật, rồi nghiệm thu các nhánh thanh toán và gia hạn.
5. Review PR #29 riêng: nhánh reading-access cũ trùng với phần đã có trên main; không merge cưỡng bức để tránh ghi đè code mới.

Nhắc đọc, chuỗi đọc và điểm/thành tích là yêu cầu mở rộng Mobile; không có mã task riêng trong Sprint 2–3 của workbook hiện tại, cần bổ sung backlog nếu muốn nghiệm thu chính thức.

## Bổ sung PB28–29 trên Web

- `ReadingProgressApiClient`: đọc/lưu/xóa vị trí và lấy toàn bộ danh sách phân trang từ API.
- `StoriesController`: Đọc tiếp lấy chương từ API; trang đọc lấy vị trí cùng chương, không ghi đè trong GET. Lỗi tiến độ không chặn đọc nội dung.
- `LibraryController`: danh sách và xóa dùng API; POST SaveProgress yêu cầu đăng nhập, antiforgery và kiểm tra đầu vào. Không nhận UserId từ trình duyệt.
- `reading-progress.js`: khôi phục phần trăm, debounce 700ms, gửi tuần tự, keepalive khi rời trang; báo lỗi riêng. Không ghi số 0 khi tải tiến độ lỗi và người dùng chưa cuộn. Không kéo người dùng khỏi #comments.
- Danh sách không còn nhãn dữ liệu mẫu. DemoReadingProgressStore chỉ còn phục vụ phần Admin thành viên cũ (PB35–36 chưa xử lý trong đợt này).
- `dotnet run --project tests/WebPaymentChecks`: 38 PASS, gồm HTTP vào MVC thật, cookie, antiforgery, đồng bộ hai chiều và danh sách hơn 50 truyện.
- `node tests/WebPaymentChecks/reading-progress.test.cjs`: PASS các ca restore, token, debounce, mapping phần trăm, bảo toàn khi tải lỗi, #comments và phản hồi HTML khi hết phiên. Đây là kiểm tra DOM giả lập, chưa thay thế nghiệm thu browser thực tế.
- Build Web đạt, còn warning nullable có sẵn trong Views/Shared/_Layout.cshtml.

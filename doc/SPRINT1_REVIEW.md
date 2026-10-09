# Kiểm tra Sprint 1 — 06/10/2026

Phạm vi: PB41, PB01–08 (không PB09), PB10–12 theo bảng phân công.
Nền so sánh: origin/main 3f2aabc. Không thay đổi dữ liệu MySQL của nhóm.

## Những phần đã sửa trong lượt này

- Giải quyết conflict còn nằm trong source sau merge tài khoản.
- Giữ migration MySQL InitialSprint1 để máy đã tạo DB tiếp tục sử dụng; bỏ bản migration SQLite thay thế và các file SQLite khỏi source.
- Khôi phục build Android, thống nhất API HTTP 5283 cho Web và emulator; HTTPS vẫn có cổng 7188.
- Giữ phân quyền Admin của PR tài khoản. CreatedBy lấy từ tài khoản đăng nhập.
- Bổ sung danh sách quản trị gồm cả truyện ẩn/nháp và tải đủ các trang.
- Sửa đăng ký mobile theo response của API (đăng ký trả user, đăng nhập mới trả token); hiện lỗi validation; đăng xuất gọi API thu hồi phiên.
- Nối chi tiết truyện mobile sang danh sách chương và màn đọc; khóa nội dung theo IsLocked.
- Chặn upload tổng nội dung trên 20MB và trên 500 chương; kiểm tra dung lượng trong lúc đọc/giải nén.
- Giữ mật khẩu DB trong User Secrets; không reset tài khoản hiện có.

## Kết quả và việc còn lại

| Phần | Hiện trạng | Cần làm trước khi chốt sprint |
|---|---|---|
| PB41 backend dùng chung | API/Web build được; Android build được; model khớp migration MySQL | Mỗi thành viên chạy lại với User Secrets/MySQL riêng; không dùng SQLite migration |
| PB01 đăng ký | Kiểm thử tạo Member, trùng email qua; Web/Mobile có code gọi API | Bấm đăng ký từ Web và emulator, thử email mới |
| PB02 đăng nhập/đăng xuất | Member mới đăng ký đăng nhập được; sai mật khẩu bị chặn; logout thu hồi token | Kiểm tra UI và trạng thái sau đóng/mở app |
| PB03 phân quyền | HTTP test: khách tạo truyện bị 401; Member sửa truyện/tạo chương bị 403; Admin tạo chương được | Kiểm tra menu Admin chỉ xuất hiện đúng vai trò |
| PB04–06 truyện/thể loại | Có API tạo/sửa, gán thể loại và Web quản trị; sửa danh sách Admin để thấy truyện ẩn/nháp | Nghiệm thu tạo/sửa truyện và gán nhiều thể loại trên Web |
| PB07–08 chương/upload | Có API/Web; test tạo, sửa/đổi số, trùng số, xem trước, lưu nhiều chương, file quá lớn qua | Thử upload .txt/.zip và sửa/xóa từ màn quản trị Web |
| PB10 miễn phí/trả phí | Test nội dung Paid không lộ; Admin được xem trước | Thử đổi Free/Paid từ Web rồi kiểm tra ở mobile |
| PB11–12 xem/đọc | API lọc nháp/ẩn, đọc miễn phí qua; mobile đã nối màn danh sách/đọc | Chạy emulator bấm từ trang chủ đến đọc, chương trước/sau và trường hợp không có chương |

Chưa tuyên bố Sprint 1 hoàn tất 100%: đã build và kiểm thử backend, chưa nghiệm thu trực tiếp toàn bộ luồng giao diện Web/Android.
Chưa phát hiện cần viết lại một nhóm API Sprint 1 từ đầu; phần còn lại chủ yếu là nghiệm thu tích hợp và xử lý lỗi nếu phát sinh.

## Không tính là hoàn thành trong Sprint 1

- Mua truyện, thanh toán và cấp quyền gói: chưa có trong 5 bảng nền.
- Đổi mật khẩu trên Web còn placeholder; không thuộc danh sách Sprint 1 đã phân công.
- Xóa cả bộ truyện trên Web vẫn báo chưa có API. Nếu nhóm muốn đưa xóa truyện vào Sprint 1 thì phải bổ sung riêng; PB04–06 hiện chỉ yêu cầu tạo/sửa/gán thể loại.
- Các chức năng theo dõi, bình luận, thống kê, thông báo không được xác nhận hoàn tất ở lượt kiểm tra này.

## Cách chạy kiểm thử backend

Chạy từ thư mục repo:

    dotnet run --project tests/Sprint1Checks/Sprint1Checks.csproj

Dùng ConnectionStrings:DefaultConnection trong User Secrets của API.
Tạo database readmanager_test_<mã ngẫu nhiên>, chạy migration và test, rồi chỉ xóa database thử đó.
Tài khoản MySQL dùng để chạy test cần quyền tạo/xóa database thử.
Không chạy đồng thời nhiều bản test vì server thử dùng cổng 5297.

## Cách chạy dự án

1. Bật MySQL. Trong VS, API → Manage User Secrets: cấu hình connection string của máy mình.
2. Nếu chưa có bảng, Package Manager Console:

       Update-Database -Project ReadManager.Api -StartupProject ReadManager.Api -Context AppDbContext -Args '--environment Development'

   Nếu DB hiện có InitialSprint1 thì không xóa/reset DB và không tạo InitialCreate khác.
3. Chạy API bằng profile http, hoặc:

       dotnet run --project src/ReadManager.Api --launch-profile http

   Swagger: http://localhost:5283/swagger
4. Chạy ReadManager.Web bằng VS hoặc terminal riêng:

       dotnet run --project src/ReadManager.Web --launch-profile http

5. Mobile: chọn ReadManager.Mobile → Android emulator → F5. API phải tiếp tục chạy.
   Android gọi http://10.0.2.2:5283/api/; Windows gọi http://localhost:5283/api/.
6. Dùng email/mật khẩu vừa đăng ký để thử Member; không dùng username vào ô Email.
   Không thể suy ra mật khẩu gốc từ PasswordHash.

# API gói đọc — PB17–18

API dùng MySQL thông qua AppDbContext; sử dụng các bảng ReadingPlans, ReadingPlanVersions và PlanVersionStories đã có. Không cần migration mới cho phần API này.

## Endpoint

Các đường dẫn dưới đây bắt đầu bằng /api/reading-plans.

| Method | Đường dẫn | Quyền | Kết quả |
| --- | --- | --- | --- |
| GET | /?page=1&pageSize=20 | Công khai | Danh sách gói đang bán và phiên bản hiện hành |
| GET | /{id} | Công khai | Chi tiết gói đang bán |
| GET | /admin | Admin | Danh sách bao gồm gói ngừng bán; có phân trang |
| GET | /{id}/admin | Admin | Chi tiết kể cả gói ngừng bán |
| GET | /{id}/versions | Admin | Lịch sử phiên bản, mới nhất trước |
| POST | / | Admin | Tạo gói cùng phiên bản 1; trả 201 |
| POST | /{id}/versions | Admin | Tạo phiên bản mới; trả 201 |
| PATCH | /{id}/name | Admin | Đổi tên; trả 204 |
| PATCH | /{id}/availability | Admin | Ngừng/bán lại; trả 204 |

Endpoint Admin cần header Authorization: Bearer <accessToken> lấy từ API đăng nhập hiện có. Chưa đăng nhập trả 401; Member gọi endpoint Admin trả 403; dữ liệu sai trả 400; gói không tồn tại trả 404. Gói ngừng bán trả 404 ở endpoint chi tiết công khai.

## Tạo gói

POST /api/reading-plans

```json
{
  "name": "Gói truyện chọn lọc",
  "price": 50000,
  "durationMonths": 1,
  "scopeType": "Selected",
  "storyIds": [1, 2]
}
```

Thay 1, 2 bằng StoryId thực sự có trong DB. Gói All gửi scopeType = "All" và storyIds = [].

Đổi giá/thời hạn/phạm vi: POST /api/reading-plans/{id}/versions với cùng nội dung trên nhưng bỏ name. Không sửa trực tiếp phiên bản cũ.

Đổi tên: PATCH /api/reading-plans/{id}/name với {"name":"Tên mới"}.

Ngừng bán: PATCH /api/reading-plans/{id}/availability với {"isAvailable":false}; bán lại gửi true.

## Dữ liệu trả về và quy tắc

- Danh sách trả {items, totalCount, page, pageSize}; page tối thiểu 1, pageSize giới hạn 1–100.
- Mỗi gói trả planId, name, isAvailable, createdAt, currentVersion.
- currentVersion gồm planVersionId, versionNumber, price, durationMonths, scopeType, createdAt, publishedAt, storyIds.
- Tên bắt buộc, tối đa 150 ký tự; khoảng trắng đầu/cuối bị loại bỏ.
- Giá dương, tối đa 2 chữ số thập phân; thời hạn 1–120 tháng.
- Selected cần ít nhất một truyện tồn tại; tối đa 1000 ID, không trùng và phải dương. All không nhận ID truyện.
- Gói mới được bán ngay, phiên bản mới công bố ngay. Không có luồng bản nháp trong API này.
- Phiên bản hiện hành là phiên bản có số lớn nhất đã công bố. Phiên bản cũ giữ nguyên giá, thời hạn và danh sách truyện.
- Hai yêu cầu tạo phiên bản đồng thời được tuần tự hóa bằng khóa hàng MySQL trong transaction để tránh trùng số phiên bản.
- Đổi tên chỉ đổi tên chung của gói; tên không được lưu riêng theo phiên bản.
- Ngừng bán không xóa gói, phiên bản hay dữ liệu quyền đã cấp. Không có endpoint xóa gói.
- All mô tả phạm vi toàn bộ truyện; Selected mô tả danh sách cố định của phiên bản. API cấp quyền đọc vẫn phải kiểm tra trạng thái hiển thị/xuất bản của truyện/chương.

## Phần các bạn khác cần nối tiếp

Frontend Web/Mobile gọi endpoint công khai để hiển thị gói; màn hình quản trị gọi endpoint Admin kèm token.

Backend đơn hàng phải tự lấy giá từ DB, kiểm tra gói đang bán và phiên bản hợp lệ, lưu PlanVersionId vào đơn. Không tin giá client gửi. Backend thanh toán/cấp quyền phải sử dụng phiên bản đã lưu trong đơn; ngừng bán gói không tự thu hồi quyền đang còn hạn.

API này chưa triển khai mua gói, thanh toán, gia hạn, cấp quyền đọc hay giao diện. Không tính các chức năng đó đã hoàn thành chỉ vì API gói đọc có sẵn.

## Kiểm thử

Tại thư mục gốc repo:

```powershell
dotnet build src/ReadManager.Api/ReadManager.Api.csproj
dotnet run --project tests/ReadingPlanChecks/ReadingPlanChecks.csproj
```

Cần MySQL đang chạy và ConnectionStrings:DefaultConnection đã cấu hình trong User Secrets của API. Bộ kiểm thử tạo database riêng có tiền tố rm_plans_test_, chạy migration, khởi động một API riêng và xóa database thử trong finally. Tài khoản MySQL dùng kiểm thử cần quyền tạo/xóa database thử. Không dùng database dự án để ghi dữ liệu thử. Nếu tiến trình bị dừng cưỡng bức, database thử có thể còn lại.

37 kiểm thử gồm phân quyền, dữ liệu sai, lưu MySQL, phiên bản cũ, tạo đồng thời, ngừng/bán lại và phân trang. Không cần chạy Web hoặc Android để chạy bộ kiểm thử này.

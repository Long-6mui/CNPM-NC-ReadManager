# Checklist nghiệm thu gói đọc và quyền đọc

Ngày đối chiếu: 22/10/2026. Main dbd7b28 đã merge Web PR #23 và Mobile PR #24.
API đơn hàng đã có; Web quản lý gói gọi API thật. Mua/gia hạn gói, trang gói của tôi,
thanh toán và khôi phục quyền của Web còn dùng DemoPlanStore. Không đánh dấu các luồng đó Done.
Nền DB thanh toán/quyền đọc nằm trên codex/payment-db-foundation.

## Chuẩn bị

Chạy API và Web theo SETUP_AND_RUNNING_GUIDE.md; dùng địa chỉ mà terminal in ra.
Dùng DB thử nghiệm, không dùng giao dịch thật. Chuẩn bị Admin, Member A, Member B;
truyện T1/T2 công khai có chương trả phí, T3 ẩn; một chương miễn phí.
Ghi ID các gói/đơn thử để đối chiếu. Không dùng Admin để chứng minh Member có quyền đọc:
Admin có quyền xem trước chương trả phí.
Với mỗi ca ghi: Đạt / Lỗi / Chờ API, ảnh hoặc HTTP status, kết quả DB.

## Test ngay: Web quản lý gói đã nối API

| ID | Thao tác | Kết quả cần có |
|---|---|---|
| W01 | Khách mở /Plans | Xem được danh sách gói đang bán, không cần đăng nhập |
| W02 | Khách/Member mở /Admin/Plans và gọi POST api/reading-plans | Web yêu cầu đăng nhập/từ chối; API khách 401, Member 403; DB không thêm gói |
| W03 | Admin tạo gói All, giá 50000, 1 tháng | Lưu gói và phiên bản 1; tải lại trang vẫn còn; /Plans hiện đúng |
| W04 | Admin tạo gói Selected, chọn T1 | Phiên bản có đúng T1; Web hiển thị đúng phạm vi |
| W05 | Tạo giá 0/âm, tháng 0, tên trống, Selected không chọn truyện | Báo lỗi rõ, không tạo dữ liệu dở dang |
| W06 | Đổi giá 50000 thành 70000 bằng phát hành phiên bản | Có phiên bản 2; phiên bản 1 không bị ghi đè; giá công khai là 70000 |
| W07 | Đổi tên gói | Web/API phản ánh tên mới; đơn đã tạo giữ ItemName cũ |
| W08 | Ngừng bán rồi bật bán lại | Khách không thấy/mua được khi ngừng; Admin vẫn thấy lịch sử; bật lại xuất hiện |
| W09 | Tắt API rồi tải trang gói hoặc lưu | Có thông báo lỗi; không báo lưu thành công giả |
| W10 | Tạo đơn truyện bằng nút mua ở chi tiết T1 | Chuyển Orders/Checkout, giá từ API; tạo Orders thật; không xuất hiện hai nút mua khác luồng |
| W11 | Bấm mua lại tuần tự khi có đơn Pending | Dùng lại đơn, mở chi tiết đơn hiện có |
| W12 | Member B gọi GET đơn của A | Không xem được đơn A; không rò rỉ thông tin |

Chú ý khi review: PlansApiClient chỉ lấy trang đầu tối đa 100 gói; bộ chọn truyện
Admin cũng chỉ lấy trang đầu từ AdminListAsync. Nếu dữ liệu vượt trang đầu, phải
bổ sung phân trang/tải thêm trước khi nghiệm thu yêu cầu quản lý toàn bộ dữ liệu.
VersionsAsync hiện trả danh sách rỗng khi API lỗi: không được hiểu nhầm là chưa có lịch sử.

## Test quyền đọc: chờ API thanh toán/cấp quyền, chưa thể nghiệm thu bằng nút demo

| ID | Thao tác | Kết quả cần có |
|---|---|---|
| R01 | Khách/Member chưa mua đọc chương miễn phí và trả phí | Miễn phí đọc được; API không trả nội dung chương trả phí |
| R02 | A mua T1, callback sandbox đã xác minh đúng tiền | Có Payment thành công + StoryPurchase đúng đơn; A đọc T1 trên Web/Mobile |
| R03 | B đọc T1 mà A đã mua | B vẫn bị khóa |
| R04 | A mua gói Selected chỉ chứa T1 | Đọc T1; T2 vẫn khóa |
| R05 | A mua gói All còn hạn | Đọc truyện trả phí công khai thuộc chính sách gói; T3 ẩn vẫn không công khai |
| R06 | Đưa EndsAt về trước hiện tại trong DB thử | Gói hết hiệu lực; A không đọc được bằng quyền gói |
| R07 | A vừa mua riêng T1 vừa có gói hết hạn | T1 vẫn đọc được bằng quyền mua riêng |
| R08 | Gia hạn gói đang còn hạn | Cộng thời gian từ hạn hiệu lực theo quy tắc đã chốt; không mất ngày còn lại |
| R09 | Gia hạn gói đã hết hạn | Bắt đầu từ thời điểm cấp mới, không cộng từ ngày hết hạn cũ |
| R10 | Đổi giá/phạm vi gói sau khi A đã mua | Đơn/quyền cũ giữ đúng phiên bản cũ; giao dịch mới dùng phiên bản mới |
| R11 | Gửi lại cùng callback, kể cả đồng thời | Không tạo quyền/thu tiền/cộng thời gian thêm lần nữa |
| R12 | Callback sai chữ ký, sai đơn, sai tiền hoặc sai tiền tệ | Không ghi nhận thành công/cấp quyền; có kết quả xử lý phù hợp |
| R13 | Thanh toán thất bại/hủy | Không cấp quyền; hiển thị đúng trạng thái |
| R14 | Thanh toán thành công nhưng cấp quyền lỗi; chạy phục hồi hai lần | Đối soát được; cấp đúng một lần; không thu tiền lại |
| R15 | Thu hồi quyền của A | API chặn quyền đó; quyền độc lập khác còn hiệu lực vẫn được xét |
| R16 | Đăng xuất, đăng nhập lại; khởi động lại API/Web | Đơn/quyền vẫn còn trong MySQL, không phụ thuộc bộ nhớ Web |
| R17 | Mua trên Web rồi đăng nhập cùng A trên Mobile | Hai nền tảng nhận cùng quyền từ API |
| R18 | Đơn hết hạn nhưng callback tiền thành công đến muộn | Ghi nhận tiền và đưa vào đối soát/cấp quyền theo quy tắc đã chốt, không mất giao dịch |

Không sửa Orders.Status=Paid bằng tay rồi kết luận thanh toán đã hoạt động.
Các CHECK/FK/unique của DB không thay thế kiểm tra chữ ký, quyền sở hữu và transaction ở service.

## Quy tắc tích hợp đã chốt cho phần nền

1. Orders là nguồn đơn thật, giá/tên/thời hạn chụp từ DB; client không quyết định số tiền.
2. Payments/PaymentEvents ghi nhận xác minh và chống lặp. Chỉ callback được xác minh mới đi tiếp.
3. UserSubscriptions/StoryPurchases là quyền cấp theo đơn; phải kiểm tra đúng loại đơn và trạng thái thanh toán.
4. Cấp quyền và ghi nhận thanh toán trong transaction, có khóa/chống tranh chấp; retry không cấp trùng.
5. ChapterService kiểm tra quyền còn hạn, chưa thu hồi, phạm vi đúng phiên bản; luôn giữ kiểm tra visibility/lịch xuất bản.
6. Thay DemoPlanStore khi API sẵn sàng; kết quả demo không được coi là bằng chứng giao dịch thật.

## Kiểm tra tự động

```powershell
dotnet run --project tests/ReadingPlanChecks
dotnet run --project tests/PaymentDbChecks
dotnet run --project tests/Sprint1Checks
dotnet build src/ReadManager.Web
```
Các bộ DB checks dùng DB tạm; không áp dụng migration vào readmanager chính.
Build thành công không thay thế các ca thao tác giao diện và quyền đọc ở trên.

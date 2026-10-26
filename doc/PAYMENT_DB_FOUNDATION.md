# Nền DB thanh toán và quyền đọc

## Đã bổ sung

Migration `20261021162849_AddPaymentEntitlementFoundation` chỉ tạo bảng mới,
không sửa/xóa dữ liệu bảng Orders hoặc các bảng cũ.

| Bảng | Lưu gì | Quan hệ |
|---|---|---|
| Payments | Mỗi lần thử thanh toán: nhà cung cấp, mã yêu cầu, mã giao dịch, tiền nhận, trạng thái và thời gian xác minh | Orders 1–n Payments |
| PaymentEvents | Sự kiện callback đã nhận, payload, kết quả xử lý/lỗi | Payments 1–n PaymentEvents |
| UserSubscriptions | Thời gian quyền dùng gói, thời điểm thu hồi | Orders 1–0..1 UserSubscriptions |
| StoryPurchases | Quyền mua riêng truyện, ngày cấp/hết hạn/thu hồi | Orders 1–0..1 StoryPurchases |

Người mua, truyện và phiên bản gói truy ngược từ Orders; không sao chép vào bảng quyền.
Tất cả ID dùng int; tiền decimal(12,2); ngày giờ lưu UTC.
Mã request unique; mã giao dịch unique theo provider; event unique theo payment/event ID.
OrderId unique trong mỗi bảng quyền. FK Restrict ngăn xóa mất lịch sử liên quan.
Ngày hết hạn phải sau ngày bắt đầu/cấp; tiền nhận không âm.
Succeeded yêu cầu tiền nhận, mã giao dịch và thời điểm xác minh/thành công.

## Phần chưa được DB tự bảo đảm — backend phải làm tiếp

Đây là schema, chưa phải API thanh toán hay cơ chế cấp quyền hoàn chỉnh.
- Xác minh chữ ký callback, đúng nhà cung cấp, đơn hàng, số tiền và đơn vị tiền tệ. Không tin outcome do Web gửi.
- Chỉ tạo UserSubscriptions cho đơn Plan, StoryPurchases cho đơn Story và chỉ sau thanh toán đã xác minh. FK chỉ xác nhận đơn tồn tại, không kiểm tra loại/trạng thái đơn.
- Trong một transaction: ghi nhận thanh toán, cập nhật đơn, cấp quyền. Khóa đơn khi xử lý; unique là lớp chống trùng cuối. Callback lặp phải trả kết quả cũ, không gia hạn lần nữa.
- Gia hạn nối tiếp thời hạn quyền hợp lệ hiện có; lưu phiên bản gói ở Orders. Kiểm tra ScopeType và PlanVersionStories khi đọc.
- Đơn Paid nhưng chưa có quyền phải được đối soát/phục hồi, không thu tiền lại. Không tự chuyển toàn bộ đơn Paid cũ thành quyền khi chưa đối soát.
- OrderService hiện dùng đơn Paid để suy ra sở hữu; cần chuyển sang quyền hiệu lực khi tích hợp. ChapterService cần được nối kiểm tra quyền; schema này chưa mở khóa chương trả phí.
- Web DemoPlanStore chưa được thay thế. Không báo thanh toán thật hoàn tất dựa trên dữ liệu demo.
- Payload không lưu token/bí mật hoặc dữ liệu thẻ; giới hạn dung lượng ở endpoint. ProviderEventId dùng ID ổn định từ provider; nếu provider không có ID thì quy định khóa chống trùng trước khi tích hợp.
- Các kiểm tra đối chiếu nhiều bảng trên đây thuộc service; không được bỏ qua chỉ vì đã có migration.

## Cách kiểm tra và áp dụng

Tại thư mục repo:

```powershell
dotnet run --project tests/PaymentDbChecks
dotnet run --project tests/Sprint1Checks
```
PaymentDbChecks tạo DB tạm tên rm_paytest_<GUID>, nâng từ schema main có đơn mẫu,
kiểm tra ràng buộc rồi xóa đúng DB tạm đó. Dùng cấu hình local API, không in mật khẩu.

Sau review/merge và sao lưu DB cần giữ, áp dụng migration từ Visual Studio Package Manager Console:

```powershell
Update-Database -Project ReadManager.Api -StartupProject ReadManager.Api -Context AppDbContext -Args '--environment Development'
```

Hoặc terminal:

```powershell
dotnet ef database update --project src/ReadManager.Api --startup-project src/ReadManager.Api -- --environment Development
```

Không chạy Add-Migration lại trên máy từng thành viên: pull migration đã được nhóm chốt rồi Update-Database.
Không rollback migration này khi đã có giao dịch cần giữ: Down sẽ xóa 4 bảng mới.

## Phân công tiếp

- PO: review schema/migration, gộp một bản chung và hướng dẫn cập nhật DB.
- Backend thanh toán: callback xác minh, chống lặp, cấp quyền có transaction, phục hồi lỗi.
- Backend quyền đọc/chương: đọc quyền đang hiệu lực và phạm vi gói; đồng bộ báo giá sở hữu.
- Web/Mobile: gọi API đơn hàng/thanh toán/quyền, bỏ dữ liệu demo khi API đã sẵn sàng.

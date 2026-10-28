# Mobile — hướng dẫn demo và kiểm thử (Sprint 4, PB42)

App Android **Chữ & Đêm** (.NET MAUI, `src/ReadManager.Mobile`) dùng chung API với Web.

## 1. Chuẩn bị API (làm 1 lần trên máy chạy API)
1. `git pull` nhánh main mới nhất.
2. Cập nhật database: Visual Studio → Package Manager Console (Default project = `ReadManager.Api`) → `Update-Database`; hoặc `cd src\ReadManager.Api` rồi `dotnet ef database update`.
3. Khóa bí mật thanh toán (bắt buộc cho "Thanh toán thử"):
   `dotnet user-secrets set "Payment:SecretKey" "chuoi-bat-ky-dai-dai"` (chạy trong `src\ReadManager.Api`).
4. Chuỗi kết nối MySQL: đặt bằng `dotnet user-secrets` như các sprint trước.
5. Chạy API: `dotnet run --launch-profile http` (cổng **5283**, chế độ Development — cần Development thì mới có cổng thanh toán giả lập).
6. Nếu demo trên điện thoại thật: mở cổng 5283 trong tường lửa Windows (Private network) và để điện thoại + máy tính **cùng Wi-Fi**.

## 2. Chạy / cài app
**Máy ảo Android (Emulator):** chọn thiết bị Android trong Visual Studio rồi bấm chạy. Địa chỉ mặc định `http://10.0.2.2:5283/api/` đã đúng.

**Điện thoại thật qua cáp USB:** bật Tùy chọn nhà phát triển + Gỡ lỗi USB, chọn điện thoại trong Visual Studio rồi chạy. Sau khi mở app: **Đăng nhập → "⚙ Địa chỉ máy chủ"** (hoặc Tài khoản → "⚙ Địa chỉ máy chủ") → nhập IP máy chạy API, ví dụ `192.168.1.25:5283` → app tự kiểm tra kết nối rồi lưu → **vuốt tắt hẳn app và mở lại**. (IP xem bằng `ipconfig` → IPv4 Address.)

**Tạo file APK để cài cho người khác:**
```
cd src\ReadManager.Mobile
dotnet publish -f net9.0-android -c Release
```
File nằm trong `bin\Release\net9.0-android\publish\` (tìm file `*-Signed.apk`). Copy sang điện thoại, bật "Cài từ nguồn không xác định" rồi cài. Nếu không thấy file `-Signed.apk`: Visual Studio → chuột phải project → Archive for Publishing → Distribute → Ad Hoc.
Cài xong vẫn phải đặt "Địa chỉ máy chủ" như trên.

## 3. Kịch bản demo gợi ý (7–10 phút)
1. Trang chủ / Khám phá: tìm truyện, lọc thể loại, mở một truyện.
2. Đăng ký tài khoản mới → đăng nhập.
3. Mở truyện trả phí → chương khóa 🔒 → **Mua trọn bộ** → Thanh toán thử → "✔ Thanh toán thành công" → truyện hiện "✔ Bạn đã sở hữu", chương mở khóa.
4. Tài khoản → **Gói đọc** → Mua / gia hạn một gói → thanh toán thử → Tủ sách → **Đã mua** thấy gói và truyện.
5. Đọc vài chương, cuộn giữa chương, quay lại → Tủ sách → **Đang đọc** → "Đọc tiếp".
6. Theo dõi truyện; từ Web Admin đăng chương mới → chuông 🔔 trên app hiện số mới → mở thông báo → vào đúng chương.
7. Bình luận chương, báo cáo một bình luận.
8. Từ Web Admin chuyển tài khoản sang **Hạn chế** → thử bình luận → thấy thông báo hạn chế và khung đỏ ở Tài khoản; chuyển sang **Tạm ngừng** → đăng nhập bị báo khóa.

## 4. Bảng kiểm thử (đánh dấu khi đã thử trên Android)
| # | Nhóm | Bước | Kết quả đúng |
|---|---|---|---|
| T01 | Kết nối | Tắt API rồi mở một truyện | Báo "Không kết nối được máy chủ (địa chỉ…)", không treo/crash |
| T02 | Kết nối | Đặt sai IP ở "Địa chỉ máy chủ" | Hộp thoại báo không kết nối, hỏi có vẫn lưu |
| T03 | Kết nối | Đặt đúng IP, mở lại app | Tải được danh sách truyện |
| T04 | Tài khoản | Đăng ký email mới | Đăng ký xong đăng nhập được |
| T05 | Tài khoản | Đăng nhập sai mật khẩu | Báo "Email hoặc mật khẩu không chính xác" |
| T06 | Tài khoản | Đăng nhập tài khoản **Tạm ngừng** | Báo tài khoản bị khóa |
| T07 | Tài khoản | Đăng xuất rồi đăng nhập lại | Tủ sách / thông báo hiện đúng của tài khoản đó |
| T08 | Mua | Mua trọn bộ truyện, thanh toán thành công | Đơn Paid, truyện "Đã sở hữu", đọc được chương trả phí |
| T09 | Mua | Thanh toán thất bại / hủy | Đơn không Paid, chương vẫn khóa |
| T10 | Mua | Tạo đơn rồi để quá hạn 30 phút | Đơn "Hết hạn", không thanh toán được |
| T11 | Gia hạn | Mua gói, mua lại gói khi còn hạn | Thời hạn được cộng dồn |
| T12 | Gia hạn | Gói còn ≤ 7 ngày | Màn Gói đọc hiện nhắc gia hạn |
| T13 | Quyền đọc | Tài khoản chưa mua mở chương trả phí | Hiện 🔒 và nút mua |
| T14 | Quyền đọc | Tài khoản có gói / đã mua mở chương trả phí | Đọc được |
| T15 | Tiến độ | Đọc chương, cuộn giữa chương, thoát | Tủ sách → Đang đọc có truyện, "Đọc tiếp" đúng chương |
| T16 | Tiến độ | Xóa truyện khỏi "Đang đọc" | Biến mất cả trên Web |
| T17 | Theo dõi | Theo dõi truyện, đăng chương mới ở Admin | Chuông 🔔 có số mới, bấm mở đúng chương |
| T18 | Cộng đồng | Bình luận / xóa bình luận của mình | Hiện ngay; chỉ xóa được bình luận của mình |
| T19 | Cộng đồng | Gửi >5 bình luận trong 1 phút | Báo thử quá nhiều lần |
| T20 | Cộng đồng | Báo cáo cùng nội dung 2 lần | Lần 2 báo đã báo cáo rồi |
| T21 | Hạn chế | Tài khoản **Hạn chế** bình luận | Báo bị hạn chế, vẫn đọc được |
| T22 | Điều hướng | Bấm nút back ở mọi màn con; đổi tab dưới | Về đúng màn trước; đổi tab rồi quay lại thấy trang đầu của mục |
| T23 | Điều hướng | Xoay ngang / thu nhỏ rồi mở lại app | Không mất đăng nhập, không crash |

## 5. Lỗi thường gặp
| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Đăng nhập báo "HTTP 500" | Chưa `Update-Database` (thiếu cột trạng thái tài khoản). Chạy lệnh ở mục 1 |
| "Chưa cấu hình Payment:SecretKey" | Chưa chạy `dotnet user-secrets set "Payment:SecretKey" ...` rồi khởi động lại API |
| "Không kết nối được máy chủ" | API chưa chạy, sai IP, khác Wi-Fi, hoặc tường lửa chặn cổng 5283 |
| Thanh toán thử báo không có cổng giả lập | API không chạy ở Development (dùng `--launch-profile http`) |
| Build báo thiếu file Admin* | Xóa các file `Admin*` còn sót trong Views/ViewModels, xóa `bin`/`obj` rồi Rebuild |

## 6. Giới hạn đã biết
- Cổng thanh toán thật (VNPay/MoMo) chưa tích hợp: demo dùng cổng giả lập của server (chỉ khi API chạy Development).
- `GET api/auth/me` chưa trả trạng thái tài khoản: app chỉ biết tài khoản bị hạn chế sau khi bị API chặn 403.
- Đổi mật khẩu trong app chưa có API.
- "Đọc tiếp" mở đúng chương, chưa tự cuộn về vị trí đã đọc.

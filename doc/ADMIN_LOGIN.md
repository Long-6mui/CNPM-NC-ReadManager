# Đăng nhập admin

Chạy ReadManager.Api bằng profile https (https://localhost:7188), sau đó chạy ReadManager.Web bằng profile https. Có thể cấu hình Api:BaseUrl trong cấu hình riêng của Web nếu cổng API khác. Không tắt kiểm tra chứng chỉ TLS.

Email: admin@doctruyen.local. Mật khẩu được đặt riêng trong DB trên máy Long; clone code không mang tài khoản hoặc mật khẩu sang máy khác.

Web gọi POST /api/auth/login, lưu phiên bằng cookie HttpOnly. API trả bearer token được bảo vệ bằng ASP.NET Data Protection (không phải JWT). GET /api/auth/me kiểm tra phiên, AccountStatus, Role và SecurityVersion. POST /api/auth/logout tăng SecurityVersion, đăng xuất mọi phiên API của tài khoản. Token hết hạn sau 8 giờ; Ghi nhớ đăng nhập chỉ giữ cookie qua lần đóng trình duyệt, không kéo dài hạn token.

Web kiểm tra phiên qua API mỗi request đã đăng nhập; API không hoạt động thì Web từ chối phiên. Các API quản trị sau này phải dùng [Authorize(Roles = "Admin")]. Login có giới hạn 10 lần/phút/IP; Web MVC gọi từ server nên nhiều người dùng Web có thể chung hạn mức này.

Các form quản trị vẫn dùng dữ liệu mẫu và chưa lưu DB. Đăng ký chưa triển khai. Không có đoạn đặt lại mật khẩu khi khởi động. Khi triển khai nhiều API instance cần cấu hình key ring Data Protection dùng chung và được bảo vệ.

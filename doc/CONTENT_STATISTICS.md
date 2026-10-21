# PB40 — Thống kê mức độ quan tâm nội dung (Backend 3)

Nhánh: `feature/s4-content-statistics` — chỉ API, dành cho Admin.

## Dữ liệu được thống kê
| Số liệu | Lấy từ | Ghi chú |
|---|---|---|
| Lượt đọc | bảng **ChapterReads** (mới) | Ghi khi độc giả đọc được nội dung chương (Web + Mobile). Không tính Admin xem trước, chương bị khóa, chương chưa tới giờ ra mắt. Người đã đăng nhập mở lại cùng chương trong 30 phút chỉ tính 1 lượt. |
| Theo dõi | StoryFollows | `NewFollows` = theo dõi mới trong khoảng; `TotalFollowers` = tổng hiện tại |
| Lượt mua | Orders | Chỉ đơn mua trọn bộ truyện **Paid**, tính theo `PaidAt` |
| Bình luận | Comments | Chỉ bình luận đang hiện (`Visible`) |

**Điểm quan tâm** = đọc × 1 + theo dõi mới × 5 + bình luận × 3 + lượt mua × 10
(đổi trọng số ở `ContentStatisticsService`).

## API (đều cần token Admin)
Tham số chung: `?from=2026-10-01&to=2026-10-21` (ngày giờ Việt Nam, tính cả 2 đầu). Bỏ trống = 30 ngày gần nhất, tối đa 366 ngày.

| API | Trả về |
|---|---|
| `GET api/admin/statistics/content/overview` | Tổng lượt đọc, độc giả, theo dõi, mua, bình luận + top 5 truyện, top 5 thể loại |
| `GET api/admin/statistics/content/stories?sort=interest&top=10` | Xếp hạng truyện. `sort`: interest, reads, follows, purchases, comments |
| `GET api/admin/statistics/content/stories/{storyId}/chapters` | Số liệu từng chương của 1 truyện |
| `GET api/admin/statistics/content/genres` | Số liệu theo thể loại |
| `GET api/admin/statistics/content/daily` | Từng ngày trong khoảng (ngày trống = 0) — dùng vẽ biểu đồ |

Lỗi: khoảng ngày sai → 400 kèm `message`; không phải Admin → 403; truyện không tồn tại → 404.

## Cài đặt sau khi pull
1. Stop app → Rebuild.
2. Package Manager Console (Default project = ReadManager.Api): `Update-Database`
   (migration **AddChapterReads** tạo bảng lượt đọc).
3. (Tuỳ chọn) Tạo dữ liệu demo: chạy `doc/CONTENT_STATISTICS_DEMO.sql` trong MySQL Workbench.

## Kiểm thử nhanh (Swagger)
1. Đăng nhập Admin → Authorize.
2. `overview` không có tham số → 200, có số liệu 30 ngày.
3. `?from=2026-10-21&to=2026-10-01` → 400 (from sau to).
4. `stories?sort=abc` → 400; `stories?sort=reads&top=3` → 3 truyện đọc nhiều nhất.
5. `stories/99999/chapters` → 404.
6. `daily?from=2026-10-01&to=2026-10-07` → đúng 7 phần tử.
7. Tài khoản thường gọi bất kỳ API trên → 403.
8. Ghi lượt đọc: tài khoản thường mở đọc 1 chương miễn phí 2 lần liền → `stories/{id}/chapters` chỉ tăng 1 lượt; Admin mở đọc → không tăng.

# SPRINT 4 — BACKEND 2: THỐNG KÊ HOẠT ĐỘNG (PB37 NÂNG CAO) & TRẢI NGHIỆM ĐỘC GIẢ THỰC TẾ

## 1. Các file đã triển khai & nâng cấp

- **Controllers**:
  - `src/ReadManager.Api/Controllers/ActivityStatisticsController.cs` (10 API thống kê hoạt động dành cho Admin)
  - `src/ReadManager.Api/Controllers/LibraryController.cs` (Bổ sung API `activity` và `continue-reading` cho Độc giả)
- **Services**:
  - `src/ReadManager.Api/Services/ActivityStatisticsService.cs`
  - `src/ReadManager.Api/Services/EntitlementService.cs` (Bổ sung cảnh báo gói sắp hết hạn, Reading Streak & Continue Reading)
- **DTOs**:
  - `src/ReadManager.Api/DTOs/Statistics/Activity/`:
    - `ActivityStatsQuery.cs`
    - `ActivityOverviewDto.cs`
    - `DailyActivityStatsDto.cs`
    - `TransactionStatusStatsDto.cs`
    - `PlanActivityStatsDto.cs`
    - `MemberActivitySummaryDto.cs`
    - `HourlyActivityStatsDto.cs` *(Mới — 24 khung giờ vàng đọc truyện)*
    - `TopReaderActivityDto.cs` *(Mới — Bảng xếp hạng Top Độc Giả & VIP)*
    - `ActivityFunnelDto.cs` *(Mới — Phễu chuyển đổi 5 bước)*
    - `RecentActivityEventDto.cs` *(Mới — Dòng sự kiện thời gian thực)*
  - `src/ReadManager.Api/DTOs/Library/MyReadingActivityDto.cs` *(Mới — Chuỗi ngày đọc liên tiếp & Đọc tiếp)*
  - `src/ReadManager.Api/DTOs/Subscriptions/UserSubscriptionDto.cs` *(Nâng cấp — Cảnh báo sắp hết hạn `IsExpiringSoon`, `StatusBadge`, `AlertMessage`)*
- **Kiểm thử tự động**: `tests/ActivityStatisticsChecks/Program.cs` (31 ca kiểm thử thực tế)

---

## 2. Các tính năng thực tế giống các nền tảng đọc truyện thương mại (Webnovel, Waka, Wattpad, Metruyenchu)

### A. Dành cho Quản trị viên (Admin Analytics — PB37)

| Endpoint | Dữ liệu trả về | Điểm nổi bật thực tế ngoài thị trường |
|---|---|---|
| `GET /api/admin/statistics/activity/overview` | `ActivityOverviewDto` | Ngoài các số tổng, tự động tính **% Tăng trưởng so với kỳ liền trước** (`NewMembersGrowthPercent`, `SubscriptionsGrowthPercent`, `StoryPurchasesGrowthPercent`), **Tỷ lệ gia hạn (`RenewalRate`)**, **Tỷ lệ chuyển đổi trả phí (`ConversionRate`)**, **Khung giờ vàng (`PeakReadingHour`)**, **Số gói sắp hết hạn trong 7 ngày (`ExpiringSoonSubscriptions`)** |
| `GET /api/admin/statistics/activity/daily` | `List<DailyActivityStatsDto>` | Bổ sung **DAU (`ActiveReaders`)** và tổng chương đọc mỗi ngày bên cạnh đăng ký mới, gia hạn, mua truyện, đơn hàng |
| `GET /api/admin/statistics/activity/peak-hours` | `List<HourlyActivityStatsDto>` | Phân tích 24 khung giờ (`00:00` – `23:59` giờ VN), tự động đánh dấu 3 **Khung giờ vàng (`IsPeakHour = true`)** để gợi ý giờ hẹn đăng chương (`ScheduledAt`) hiệu quả nhất |
| `GET /api/admin/statistics/activity/top-readers` | `List<TopReaderActivityDto>` | Bảng xếp hạng **Top Độc Giả & VIP ủng hộ** (`?sort=score\|reads\|spent\|comments&top=10`), tự động gắn danh hiệu: `"VIP Thuê Bao"`, `"Người Mua Truyện"`, `"Độc Giả Tích Cực"`, `"Thành Viên Mới"` |
| `GET /api/admin/statistics/activity/funnel` | `ActivityFunnelDto` | **Phễu chuyển đổi 5 bước**: Thành viên Active $\rightarrow$ Độc giả có đọc $\rightarrow$ Độc giả tương tác (Follow/Comment) $\rightarrow$ Tạo đơn hàng $\rightarrow$ Thanh toán thành công (`Paid`) |
| `GET /api/admin/statistics/activity/recent-feed` | `List<RecentActivityEventDto>` | **Live Activity Stream**: Dòng thời gian sự kiện trực tiếp (Ai vừa đăng ký, ai vừa mua gói, ai vừa gia hạn, đơn nào vừa lỗi) |
| `GET /api/admin/statistics/activity/export-csv` | File `.csv` (UTF-8 BOM) | Xuất báo cáo thống kê hoạt động chuẩn tiếng Việt mở trực tiếp bằng **Microsoft Excel** |
| `GET /api/admin/statistics/activity/transactions` | `TransactionStatusSummaryDto` | Phân tích 5 trạng thái đơn (`Paid`, `Pending`, `Failed`, `Cancelled`, `Expired`) kèm đối chiếu số lượng `Payments` |
| `GET /api/admin/statistics/activity/plans` | `List<PlanActivityStatsDto>` | Thống kê theo từng gói đọc: Đăng ký mới, Gia hạn, Đang dùng, Sắp hết hạn ($\le 7$ ngày), Đã hết hạn, Tỷ lệ gia hạn `%` |
| `GET /api/admin/statistics/activity/members` | `MemberActivitySummaryDto` | Thống kê thành viên chuyên sâu: Tỷ lệ thành viên trả phí (`PayingMemberRate`), Tỷ lệ giữ chân (`RetentionRate`), Thuê bao rời bỏ (`ChurnedSubscribers`) |

### B. Dành cho Độc giả (Member / Reader Experience — PB27)

| Endpoint | Dữ liệu trả về | Điểm nổi bật thực tế ngoài thị trường |
|---|---|---|
| `GET /api/me/library/activity` | `MyReadingActivityDto` | Tính **Chuỗi ngày đọc liên tiếp (`CurrentStreakDays`, `LongestStreakDays`)** như Kindle/Webnovel, thống kê **Top 3 thể loại yêu thích cá nhân (`FavoriteGenres`)**, danh hiệu thành viên (`MemberTier`) |
| `GET /api/me/library/continue-reading` | `List<ContinueReadingItemDto>` | Danh sách **"Đọc tiếp" (Continue Reading)**: Lưu vết chương vừa đọc gần nhất của từng bộ truyện kèm **% tiến độ hoàn thành (`ProgressPercent`)** |
| `GET /api/me/subscriptions` | `UserSubscriptionSummaryDto` | Tự động bật cảnh báo `HasExpiringAlert = true` kèm `AlertMessage` nhắc nhở khi gói đọc còn $\le 7$ ngày |

---

## 3. Cách chạy kiểm thử tự động (31 ca kiểm thử)

```powershell
dotnet run --project tests/ActivityStatisticsChecks
```

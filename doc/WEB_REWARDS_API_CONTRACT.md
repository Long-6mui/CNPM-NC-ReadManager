# Web — Ví xu, chuỗi đọc, voucher: hợp đồng API

Cập nhật 20/11/2026: API lịch sử xu đã nối backend và được kiểm thử qua RewardsApiClient. Các endpoint voucher mua truyện bên dưới tiếp tục là hợp đồng để backend voucher và đơn hàng triển khai.
Web tự ẩn/hiện thông báo "chưa bật" khi API trả 404, nên có thể gắn API từng phần mà không làm hỏng trang.

Đã có sẵn trên API (Web dùng ngay): `GET api/me/streak`, `POST api/me/streak/restore`, `GET api/me/coupons`,
`GET api/reader/profile`, `POST api/reader/freeze-cards/{requestId}`.

## 1. Lịch sử cộng/trừ xu
`GET api/reader/coin-history?page=1&pageSize=20` → `{ items, totalCount, page, pageSize }`

| Trường | Ý nghĩa |
|---|---|
| `id` (long) | mã giao dịch |
| `amount` (int) | dương = cộng xu, âm = trừ xu |
| `type` | `ChapterReward`, `StreakReward`, `FreezeCard`, `Voucher`, `AdminGrant`, `Refund` |
| `description` | ví dụ "Chương 3 — Tên truyện" |
| `balanceAfter` (long?) | số dư sau giao dịch |
| `createdAt` | UTC |

## 2. Voucher mua truyện
- `GET api/vouchers/offers` → danh sách có thể đổi: `offerId, name, description, coinCost, discountType, value, maxDiscount?, minOrderAmount?, validDays`
- `POST api/vouchers/offers/{offerId}/redeem` body `{ "requestId": "<guid>" }` → `VoucherApi`.
  Gửi lại cùng `requestId` **không** trừ xu lần hai (Web sinh sẵn GUID trong form). Thiếu xu → `409 { "message": "..." }`.
- `GET api/me/vouchers?status=Available` → `voucherId, name, discountType, value, maxDiscount?, minOrderAmount?, status, expiresAt, usedAt?`
  - `discountType`: `Percent` (value = %) hoặc `Amount` (value = VND)
  - `status`: `Available | Reserved | Used | Expired`

## 3. Áp voucher vào đơn mua truyện
- `GET api/orders/quote/story/{id}?voucherId=` → `OrderQuoteDto` thêm `originalAmount`, `discountAmount`, `voucherId`.
  `amount` = số tiền phải trả sau giảm. Voucher sai chủ/hết hạn/không đủ điều kiện → `400/409 { "message": "..." }`.
- `POST api/orders` body `{ "orderType": "Story", "storyId": 4, "voucherId": 1 }` — Web **không** gửi giá, backend tự tính.
- `OrderDto` thêm `originalAmount`, `discountAmount`, `voucherId`, `voucherName` để trang chi tiết đơn hiện giá gốc / giảm / tổng.

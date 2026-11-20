# Chuỗi đọc và ví xu dùng chung

Web và Mobile dùng cùng token tài khoản và API. Ngày đọc tính theo UTC+7.
Bản này bổ sung migration AddCoinLedgerAndQualifiedReading.

## Điều kiện ghi nhận

- POST /api/reader/chapters/{chapterId}/start trả ticket cho tài khoản có quyền đọc chương đã công khai.
- POST /api/reader/chapters/{chapterId}/finish nhận JSON ticket và percent.
- Ticket đúng tài khoản/chương, tồn tại từ 60 giây đến 24 giờ, thuộc ngày hiện tại UTC+7. Percent từ 99 đến 100.
- Client chỉ gửi finish khi đã đọc tới cuối và giữ trang đọc hoạt động. Server kiểm tra thời gian ticket và phần trăm client gửi. Đây là kiểm tra điều kiện kỹ thuật, không xác minh người dùng thực sự đọc từng dòng.
- Mở chương chỉ ghi lượt truy cập. Finish hợp lệ ghi ngày đọc.
- Mỗi chương thưởng 10 xu một lần trong đời tài khoản. Đọc lại chương đã thưởng vẫn được ghi ngày đọc.
- Mỗi ngày chỉ tăng chuỗi một lần. Từ ngày thứ hai của chuỗi thưởng thêm 5 xu/ngày.
- Ticket từ ngày trước trả 400. Client mở lại chương để lấy ticket mới.
- Finish trả granted (bool, có nhận 10 xu chương), coinsAdded (tổng xu nhận trong lần gọi), coins (số dư), currentStreak.
- Web/Mobile hiển thị coinsAdded. Giá trị có thể là 0, 5, 10 hoặc 15.
- GET /api/me/streak trả trạng thái, lịch 7 ngày và phần thưởng. Lịch mới dựa trên ngày finish hợp lệ.

## Giữ chuỗi

Đổi 50 xu lấy một thẻ qua POST /api/reader/freeze-cards/{requestId}.
RequestId là GUID tạo cho một ý định đổi. Khi retry phải dùng lại GUID đó.
Tối đa giữ 5 thẻ. Thiếu xu hoặc đầy thẻ trả 409 với message.
Trừ xu và cấp thẻ cùng một transaction. Hai nền tảng mua đồng thời dùng chung số dư.
Lỡ đúng một ngày và có thẻ thì tự tiêu một thẻ khi hệ thống đánh giá chuỗi.
Thẻ giữ số ngày hiện có. Ngày được thẻ bảo vệ không tạo thưởng xu.
Lỡ từ hai ngày hoặc hết thẻ thì chuỗi đứt, xu đã nhận được giữ trong ví.
Quy tắc khôi phục chuỗi và các mốc thưởng hiện có tiếp tục áp dụng.

## Số dư và lịch sử

GET /api/reader/profile trả coins, freezeCards, prompted, genres.
coins là số nguyên 64-bit.

GET /api/reader/coins/history?page=1&pageSize=20 yêu cầu đăng nhập.
Chỉ trả lịch sử của tài khoản trong token.
page >= 1 (tối đa 1000000), pageSize từ 1 đến 100.
Kết quả gồm items, totalCount, page, pageSize.
Mỗi item có coinTransactionId, amount, balanceAfter, kind, description, createdAt.
amount dương là cộng xu, âm là trừ xu. createdAt là UTC.
Sắp xếp theo coinTransactionId giảm dần để giao dịch mới nhất ở đầu.

Các kind hiện có
- OpeningBalance — số dư trước khi bổ sung lịch sử.
- ChapterReward — thưởng hoàn thành chương.
- StreakReward — thưởng giữ chuỗi.
- AdminGrant — quản trị viên cộng xu.
- FreezeCardPurchase — đổi thẻ bảo vệ chuỗi.

Migration tạo một OpeningBalance cho tài khoản cũ từ công thức ví cũ.
Dữ liệu trước đây thiếu thời gian chi tiết nên OpeningBalance phản ánh tổng số dư khi nâng cấp.
Ngày gần nhất của chuỗi cũ được giữ lại. Lịch đầy đủ được ghi từ các lần đọc hợp lệ mới.
CoinTransactions là nguồn tính số dư. Không sửa EarnedStreakCoins bằng SQL để cộng xu.
Dùng API quản trị đã có để cộng xu khi demo.

## Tích hợp đổi voucher

Service voucher mở transaction bằng cùng AppDbContext và khóa dòng Users của tài khoản bằng SELECT ... FOR UPDATE.
Sau khi khóa, gọi CoinLedger.AddAsync(db, userId, -cost, sourceKey, kind, description).
SourceKey phải ổn định theo request đổi voucher, ví dụ voucher:{requestId}.
AddAsync trả false nếu giao dịch cùng key/amount/kind đã tồn tại.
Khi đó trả lại kết quả đã tạo của request, tránh cấp voucher thêm.
Gọi AddAsync và lưu voucher trong cùng transaction, chỉ commit khi cả hai thành công.
Thiếu xu ném InvalidOperationException. Service chuyển thành lỗi 409 có message.
Một lần đổi mới dùng requestId mới. API xác định giá bằng cấu hình server.
Ledger tự lưu số dư sau giao dịch, lịch sử ví sẽ hiển thị khoản đổi voucher.
Không mở endpoint cho client tự truyền số xu cần trừ.

## Cập nhật DB và kiểm thử

Trong Package Manager Console

    Update-Database -Project ReadManager.Api -StartupProject ReadManager.Api -Context AppDbContext -Args '--environment Development'

Khởi động lại API sau cập nhật.
Bộ kiểm thử tests/WebPaymentChecks dùng MySQL tạm riêng, kiểm tra nâng cấp dữ liệu cũ,
Web/Mobile hoàn thành cùng lúc, thưởng theo ngày, đổi thẻ đồng thời, bảo vệ chuỗi và lịch sử xu.

-- =====================================================================
-- PB40 — DỮ LIỆU DEMO CHO THỐNG KÊ NỘI DUNG (BE3)
-- Tạo ngẫu nhiên trong 30 ngày gần nhất, dựa trên truyện / chương / tài khoản ĐANG CÓ:
--   lượt đọc, theo dõi, bình luận, đơn mua truyện đã thanh toán (Paid).
-- Cách chạy: MySQL Workbench → mở file này → bấm ⚡ (Execute All).
-- Chạy lại nhiều lần được (mỗi lần cộng thêm dữ liệu). Cần MySQL 8 trở lên.
-- Điều kiện: đã chạy Update-Database (có bảng ChapterReads, Orders, Comments, StoryFollows),
--            có ít nhất 1 truyện Public có chương Published và vài tài khoản.
-- =====================================================================
USE readmanager;

SET @days = 30;   -- rải dữ liệu trong bao nhiêu ngày gần đây

-- ---------- 1. LƯỢT ĐỌC ----------
-- Mỗi chương công khai: khoảng 20 lượt đọc, 1/4 là khách chưa đăng nhập (UserId = NULL)
INSERT INTO ChapterReads (StoryId, ChapterId, UserId, ReadAt)
WITH RECURSIVE seq AS (SELECT 1 AS n UNION ALL SELECT n + 1 FROM seq WHERE n < 40),
users AS (SELECT UserId, ROW_NUMBER() OVER (ORDER BY UserId) AS rn, COUNT(*) OVER () AS cnt FROM Users)
SELECT c.StoryId,
       c.ChapterId,
       CASE WHEN MOD(seq.n, 4) = 0 THEN NULL ELSE u.UserId END,
       UTC_TIMESTAMP() - INTERVAL FLOOR(RAND() * @days * 1440) MINUTE
FROM Chapters c
JOIN Stories s ON s.StoryId = c.StoryId
CROSS JOIN seq
JOIN users u ON u.rn = 1 + MOD(seq.n + c.ChapterId, u.cnt)
WHERE c.PublicationStatus = 'Published'
  AND s.Visibility = 'Public'
  AND (c.ScheduledAt IS NULL OR c.ScheduledAt <= UTC_TIMESTAMP())
  AND RAND() < 0.5;

-- ---------- 2. THEO DÕI ----------
-- Mỗi tài khoản có khoảng 50% khả năng theo dõi từng truyện (đã theo dõi rồi thì bỏ qua)
INSERT IGNORE INTO StoryFollows (UserId, StoryId, CreatedAt)
SELECT u.UserId, s.StoryId, UTC_TIMESTAMP() - INTERVAL FLOOR(RAND() * @days * 1440) MINUTE
FROM Users u
CROSS JOIN Stories s
WHERE s.Visibility = 'Public' AND RAND() < 0.5;

-- ---------- 3. BÌNH LUẬN ----------
INSERT INTO Comments (UserId, StoryId, ChapterId, Content, Status, CreatedAt)
SELECT u.UserId, c.StoryId, c.ChapterId,
       ELT(1 + FLOOR(RAND() * 6), 'Hay quá!', 'Hóng chương sau', 'Tình tiết cuốn ghê',
           'Tác giả viết hay thật', 'Đọc tới đây xúc động quá', 'Ra chương mới nhanh nha'),
       'Visible',
       UTC_TIMESTAMP() - INTERVAL FLOOR(RAND() * @days * 1440) MINUTE
FROM Chapters c
JOIN Stories s ON s.StoryId = c.StoryId
CROSS JOIN Users u
WHERE c.PublicationStatus = 'Published'
  AND s.Visibility = 'Public'
  AND (c.ScheduledAt IS NULL OR c.ScheduledAt <= UTC_TIMESTAMP())
  AND RAND() < 0.3;

-- ---------- 4. LƯỢT MUA TRỌN BỘ (đơn đã thanh toán) ----------
-- Chỉ truyện trả phí có giá; mỗi tài khoản mua 1 truyện tối đa 1 lần
INSERT INTO Orders (UserId, OrderType, StoryId, ItemName, Amount, Status, CreatedAt, UpdatedAt, PaidAt)
SELECT x.UserId, 'Story', x.StoryId, x.Title, x.CurrentPrice, 'Paid', x.t, x.t, x.t
FROM (
    SELECT u.UserId, s.StoryId, s.Title, s.CurrentPrice,
           UTC_TIMESTAMP() - INTERVAL FLOOR(RAND() * @days * 1440) MINUTE AS t
    FROM Users u
    CROSS JOIN Stories s
    WHERE s.Visibility = 'Public'
      AND s.AccessPolicy <> 'Free'
      AND s.CurrentPrice > 0
      AND RAND() < 0.4
      AND NOT EXISTS (SELECT 1 FROM Orders o
                      WHERE o.UserId = u.UserId AND o.StoryId = s.StoryId AND o.Status = 'Paid')
) AS x;

-- ---------- Kiểm tra nhanh ----------
SELECT 'ChapterReads' AS Bang, COUNT(*) AS SoDong FROM ChapterReads
UNION ALL SELECT 'StoryFollows', COUNT(*) FROM StoryFollows
UNION ALL SELECT 'Comments', COUNT(*) FROM Comments
UNION ALL SELECT 'Orders (Paid)', COUNT(*) FROM Orders WHERE Status = 'Paid';

-- ---------- Muốn xóa lượt đọc demo để làm lại (bỏ dấu -- rồi chạy) ----------
-- DELETE FROM ChapterReads;

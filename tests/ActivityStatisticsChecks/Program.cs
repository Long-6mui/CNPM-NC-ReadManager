using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Auth;
using ReadManager.Api.DTOs.Statistics.Activity;
using ReadManager.Api.Entities;
using ReadManager.Api.Services;

Console.WriteLine("==========================================================");
Console.WriteLine("  KIỂM THỬ SPRINT 4 — BACKEND 2 (PB37 NÂNG CAO & QUYỀN ĐỌC)");
Console.WriteLine("==========================================================");

int passed = 0;
void Check(bool ok, string name)
{
    if (!ok)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("FAIL: " + name);
        Console.ResetColor();
        throw new Exception("FAIL: " + name);
    }
    passed++;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"PASS [{passed}]: {name}");
    Console.ResetColor();
}

var config = new ConfigurationBuilder()
    .AddUserSecrets("7fb0539b-fdcb-468b-a959-03ab550ad094")
    .AddEnvironmentVariables()
    .Build();

var rawCs = config.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;User=root;Password=root;";

var cs = new MySqlConnectionStringBuilder(rawCs);
var testDb = "rm_s4_act_" + Guid.NewGuid().ToString("N")[..12];
cs.Database = testDb;

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseMySql(cs.ConnectionString, ServerVersion.AutoDetect(cs.ConnectionString))
    .Options;

await using var db = new AppDbContext(options);

try
{
    await db.Database.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "Schema MySQL khớp hoàn toàn với Migrations");

    var now = DateTime.UtcNow;

    // =====================================================================
    // PHẦN 1: KIỂM THỬ LẠI TÀI KHOẢN, QUYỀN ĐỌC & TIẾN ĐỘ ĐỌC CÁ NHÂN
    // =====================================================================
    var authService = new AuthService(db, new EphemeralDataProtectionProvider());
    var reg1 = await authService.RegisterAsync(new RegisterRequestDto
    {
        Email = "member1@s4.test",
        DisplayName = "Thành viên 1",
        Password = "Password_123!"
    });
    Check(reg1.Success && reg1.User!.Role == "Member", "Đăng ký tài khoản thành viên mới thành công");

    var dupReg = await authService.RegisterAsync(new RegisterRequestDto
    {
        Email = "member1@s4.test",
        DisplayName = "Trùng email",
        Password = "Password_123!"
    });
    Check(!dupReg.Success, "Chặn đăng ký trùng email");

    var loginOk = await authService.LoginAsync(new LoginRequestDto
    {
        Email = "member1@s4.test",
        Password = "Password_123!"
    });
    Check(loginOk.Success && !string.IsNullOrEmpty(loginOk.Data?.AccessToken), "Đăng nhập tài khoản hợp lệ trả về AccessToken");

    var loginFail = await authService.LoginAsync(new LoginRequestDto
    {
        Email = "member1@s4.test",
        Password = "WrongPassword!"
    });
    Check(!loginFail.Success, "Chặn đăng nhập sai mật khẩu");

    var member1 = await db.Users.SingleAsync(u => u.Email == "member1@s4.test");
    var member2 = new User
    {
        Username = "member2",
        Email = "member2@s4.test",
        DisplayName = "Thành viên 2 (đăng ký 45 ngày trước)",
        Role = "Member",
        AccountStatus = "Active",
        PasswordHash = "hash",
        CreatedAt = now.AddDays(-45)
    };
    var lockedMember = new User
    {
        Username = "locked_user",
        Email = "locked@s4.test",
        DisplayName = "Tài khoản bị khóa",
        Role = "Member",
        AccountStatus = "Locked",
        PasswordHash = "hash",
        CreatedAt = now.AddDays(-5)
    };
    var admin = new User
    {
        Username = "admin_s4",
        Email = "admin@s4.test",
        DisplayName = "Quản trị viên",
        Role = "Admin",
        AccountStatus = "Active",
        PasswordHash = "hash",
        CreatedAt = now.AddDays(-60)
    };
    db.Users.AddRange(member2, lockedMember, admin);
    await db.SaveChangesAsync();

    var genre = new Genre { Name = "Huyền Huyễn", Slug = "huyen-huyen" };
    db.Genres.Add(genre);
    await db.SaveChangesAsync();

    var storyPaid = new Story
    {
        Title = "Truyện Trả Phí S4",
        Slug = "truyen-tra-phi-s4",
        AuthorName = "Tác giả S4",
        Synopsis = "Tóm tắt",
        Visibility = "Public",
        AccessPolicy = "Paid",
        CurrentPrice = 45000,
        CreatedBy = admin.UserId
    };
    var storyPlan = new Story
    {
        Title = "Truyện Gói S4",
        Slug = "truyen-goi-s4",
        AuthorName = "Tác giả S4",
        Synopsis = "Tóm tắt",
        Visibility = "Public",
        AccessPolicy = "Mixed",
        CreatedBy = admin.UserId
    };
    db.Stories.AddRange(storyPaid, storyPlan);
    await db.SaveChangesAsync();

    db.StoryGenres.Add(new StoryGenre { StoryId = storyPaid.StoryId, GenreId = genre.GenreId });
    await db.SaveChangesAsync();

    var chPaid = new Chapter
    {
        StoryId = storyPaid.StoryId,
        ChapterNumber = 1,
        Title = "Chương 1 VIP",
        Content = "Nội dung chương trả phí",
        AccessLevel = "Paid",
        PublicationStatus = "Published"
    };
    var chPlan = new Chapter
    {
        StoryId = storyPlan.StoryId,
        ChapterNumber = 1,
        Title = "Chương 1 Gói",
        Content = "Nội dung chương gói",
        AccessLevel = "Paid",
        PublicationStatus = "Published"
    };
    db.Chapters.AddRange(chPaid, chPlan);
    await db.SaveChangesAsync();

    var plan = new ReadingPlan { Name = "Gói Tháng S4", IsAvailable = true };
    db.ReadingPlans.Add(plan);
    await db.SaveChangesAsync();

    var planVer = new ReadingPlanVersion
    {
        PlanId = plan.PlanId,
        VersionNumber = 1,
        Price = 50000,
        DurationMonths = 1,
        ScopeType = "All",
        PublishedAt = now.AddDays(-60)
    };
    db.ReadingPlanVersions.Add(planVer);
    await db.SaveChangesAsync();

    var entitlementService = new EntitlementService(db);
    var chapterService = new ChapterService(db, new NotificationService(db), entitlementService);

    var beforeBuy = await chapterService.GetByIdAsync(chPaid.ChapterId, isAdmin: false, readerId: member1.UserId);
    Check(beforeBuy!.IsLocked && string.IsNullOrEmpty(beforeBuy.Content), "Chương trả phí ẩn nội dung khi độc giả chưa mua");

    // Member 1 mua lẻ truyện storyPaid
    var orderStory1 = new Order
    {
        UserId = member1.UserId,
        OrderType = "Story",
        StoryId = storyPaid.StoryId,
        ItemName = storyPaid.Title,
        Amount = 45000,
        Status = "Paid",
        CreatedAt = now.AddDays(-2),
        PaidAt = now.AddDays(-2)
    };
    db.Orders.Add(orderStory1);
    await db.SaveChangesAsync();
    await entitlementService.GrantStoryPurchaseAsync(orderStory1.OrderId);

    var afterBuy = await chapterService.GetByIdAsync(chPaid.ChapterId, isAdmin: false, readerId: member1.UserId);
    Check(!afterBuy!.IsLocked && afterBuy.Content == "Nội dung chương trả phí", "PB23: Mua lẻ truyện mở khóa đọc chương thành công");

    // Thêm lượt đọc hôm qua để kiểm tra chuỗi ngày đọc liên tiếp (Reading Streak = 2 ngày)
    db.ChapterReads.Add(new ChapterRead
    {
        StoryId = storyPlan.StoryId,
        ChapterId = chPlan.ChapterId,
        UserId = member1.UserId,
        ReadAt = now.AddDays(-1)
    });
    await db.SaveChangesAsync();

    var myActivity = await entitlementService.GetMyReadingActivityAsync(member1.UserId);
    Check(myActivity.CurrentStreakDays >= 2 && myActivity.ContinueReading.Count == 2 && myActivity.FavoriteGenres.Contains("Huyền Huyễn"),
          "Thực tế (Reader): Tính đúng Chuỗi ngày đọc liên tiếp (Streak >= 2), Đọc tiếp (Continue Reading) & Thể loại yêu thích");

    // Member 1 mua gói lần 1 (Đăng ký mới) và gia hạn lần 2 (Gia hạn)
    var orderPlan1 = new Order
    {
        UserId = member1.UserId,
        OrderType = "Plan",
        PlanVersionId = planVer.PlanVersionId,
        ItemName = "Gói Tháng S4 (1 tháng)",
        Amount = 50000,
        DurationMonths = 1,
        Status = "Paid",
        CreatedAt = now.AddDays(-3),
        PaidAt = now.AddDays(-3)
    };
    db.Orders.Add(orderPlan1);
    await db.SaveChangesAsync();
    var sub1 = await entitlementService.GrantSubscriptionAsync(orderPlan1.OrderId);

    var orderPlan1Renew = new Order
    {
        UserId = member1.UserId,
        OrderType = "Plan",
        PlanVersionId = planVer.PlanVersionId,
        ItemName = "Gói Tháng S4 (1 tháng)",
        Amount = 50000,
        DurationMonths = 1,
        Status = "Paid",
        CreatedAt = now.AddDays(-1),
        PaidAt = now.AddDays(-1)
    };
    db.Orders.Add(orderPlan1Renew);
    await db.SaveChangesAsync();
    var sub2 = await entitlementService.GrantSubscriptionAsync(orderPlan1Renew.OrderId);
    Check(sub2.StartsAt >= sub1.EndsAt.AddSeconds(-5), "Gia hạn gói nối tiếp thời hạn EndsAt của gói đang còn hạn");

    // Member 2 đã mua gói từ 40 ngày trước và gia hạn hôm nay
    var orderMember2Old = new Order
    {
        UserId = member2.UserId,
        OrderType = "Plan",
        PlanVersionId = planVer.PlanVersionId,
        ItemName = "Gói Tháng S4",
        Amount = 50000,
        DurationMonths = 1,
        Status = "Paid",
        CreatedAt = now.AddDays(-40),
        PaidAt = now.AddDays(-40)
    };
    var orderMember2Renew = new Order
    {
        UserId = member2.UserId,
        OrderType = "Plan",
        PlanVersionId = planVer.PlanVersionId,
        ItemName = "Gói Tháng S4",
        Amount = 50000,
        DurationMonths = 1,
        Status = "Paid",
        CreatedAt = now.AddHours(-5),
        PaidAt = now.AddHours(-5)
    };
    var orderPending = new Order
    {
        UserId = member1.UserId,
        OrderType = "Story",
        StoryId = storyPlan.StoryId,
        ItemName = storyPlan.Title,
        Amount = 30000,
        Status = "Pending",
        CreatedAt = now.AddHours(-2)
    };
    var orderFailed = new Order
    {
        UserId = member2.UserId,
        OrderType = "Story",
        StoryId = storyPaid.StoryId,
        ItemName = storyPaid.Title,
        Amount = 45000,
        Status = "Failed",
        CreatedAt = now.AddHours(-3)
    };
    var orderCancelled = new Order
    {
        UserId = member2.UserId,
        OrderType = "Plan",
        PlanVersionId = planVer.PlanVersionId,
        ItemName = "Gói Tháng S4",
        Amount = 50000,
        Status = "Cancelled",
        CreatedAt = now.AddHours(-4)
    };
    var orderExpired = new Order
    {
        UserId = member1.UserId,
        OrderType = "Plan",
        PlanVersionId = planVer.PlanVersionId,
        ItemName = "Gói Tháng S4",
        Amount = 50000,
        Status = "Expired",
        CreatedAt = now.AddDays(-2)
    };
    db.Orders.AddRange(orderMember2Old, orderMember2Renew, orderPending, orderFailed, orderCancelled, orderExpired);
    await db.SaveChangesAsync();

    // =====================================================================
    // PHẦN 2: KIỂM THỬ PB37 CƠ BẢN & CÁC TÍNH NĂNG MỞ RỘNG THỰC TẾ
    // =====================================================================
    var activityStats = new ActivityStatisticsService(db);
    var query = new ActivityStatsQuery();

    var overview = await activityStats.GetOverviewAsync(query);

    Check(overview.TotalMembers == 3, "PB37: Tổng số thành viên (Role = Member) = 3");
    Check(overview.ActiveAccounts == 2 && overview.LockedAccounts == 1, "PB37: Phân tách đúng tài khoản Active (2) và Locked (1)");
    Check(overview.NewMembers == 2, "PB37: Số thành viên đăng ký mới trong 30 ngày = 2");
    Check(overview.ActiveReaders == 1, "PB37: Số độc giả có hoạt động đọc chương trong kỳ = 1");
    Check(overview.NewSubscriptions == 1, "PB37: Đếm đúng số lượt đăng ký gói mới (lần đầu) = 1");
    Check(overview.Renewals == 2, "PB37: Đếm đúng số lượt gia hạn gói = 2");
    Check(overview.TotalPlanOrdersPaid == 3, "PB37: Tổng đơn gói thành công = Đăng ký mới + Gia hạn = 3");
    Check(overview.StoryPurchases == 1, "PB37: Số lượt mua lẻ truyện thành công trong kỳ = 1");
    Check(overview.UniqueBuyers == 2, "PB37: Số người mua duy nhất trong kỳ = 2");
    Check(overview.TotalOrders == 8, "PB37: Tổng số đơn hàng phát sinh trong kỳ = 8");
    Check(overview.PaidOrders == 4 && overview.PendingOrders == 1 && overview.FailedOrders == 1
          && overview.CancelledOrders == 1 && overview.ExpiredOrders == 1,
          "PB37: Thống kê chính xác từng trạng thái đơn (Paid=4, Pending=1, Failed=1, Cancelled=1, Expired=1)");
    Check(Math.Abs(overview.SuccessRate - 50.0) < 0.01, "PB37: Tỷ lệ giao dịch thành công = 50%");

    // Kiểm tra các chỉ số thực tế nâng cao
    Check(overview.NewMembersGrowthPercent == 100.0 && overview.SubscriptionsGrowthPercent == 200.0,
          "Thực tế (Growth %): Tính chính xác % tăng trưởng so với kỳ 30 ngày liền trước (NewMembers +100%, Subscriptions +200%)");
    Check(Math.Abs(overview.RenewalRate - 66.67) < 0.1,
          "Thực tế (Retention): Tính đúng Tỷ lệ gia hạn gói (RenewalRate = 66.67%)");
    Check(overview.PeakReadingHour.HasValue,
          "Thực tế (Peak Hour): Xác định đúng khung giờ vàng có lượt đọc cao nhất trong ngày");

    var peakHours = await activityStats.GetPeakHoursAsync(query);
    Check(peakHours.Count == 24 && peakHours.Any(h => h.IsPeakHour) && peakHours.Sum(h => h.Reads) == 2,
          "Thực tế (Peak Hours 24h): Trả về đủ 24 khung giờ đọc truyện & đánh dấu IsPeakHour chính xác");

    var topReaders = await activityStats.GetTopReadersAsync(new ActivityStatsQuery { Sort = "score", Top = 10 });
    Check(topReaders.Count == 2 && topReaders[0].Rank == 1 && topReaders[0].UserId == member1.UserId
          && topReaders[0].MemberTier == "VIP Thuê Bao",
          "Thực tế (Top Readers & VIP): Xếp hạng đúng Top độc giả và tự động gắn huy hiệu 'VIP Thuê Bao'");

    var funnel = await activityStats.GetFunnelAsync(query);
    Check(funnel.Stages.Count == 5 && funnel.TotalActiveMembers == 2 && funnel.ReadersCount == 1 && funnel.PayingUsersCount == 2,
          "Thực tế (Conversion Funnel): Phễu chuyển đổi 5 bước hoạt động chính xác");

    var recentFeed = await activityStats.GetRecentFeedAsync(15);
    Check(recentFeed.Count > 0 && recentFeed.Any(e => e.EventType == "Renewal") && recentFeed.Any(e => e.EventType == "StoryPurchase"),
          "Thực tế (Live Activity Feed): Dòng sự kiện thời gian thực ghi nhận đủ Đăng ký mới, Gia hạn, Mua truyện");

    var csvBytes = await activityStats.ExportDailyCsvAsync(query);
    var csvText = Encoding.UTF8.GetString(csvBytes);
    Check(csvBytes.Length > 100 && csvText.Contains("BÁO CÁO THỐNG KÊ HOẠT ĐỘNG (PB37)"),
          "Thực tế (Export CSV): Xuất báo cáo Excel/CSV chuẩn UTF-8 BOM tiếng Việt thành công");

    var daily = await activityStats.GetDailyAsync(query);
    Check(daily.Count == 30 && daily.Sum(d => d.ChaptersRead) == 2 && daily.Sum(d => d.TotalOrders) == overview.TotalOrders,
          "PB37: Biểu đồ ngày trả đủ 30 ngày (kèm DAU & ChaptersRead) và khớp 100% với Overview");

    var badOrderThrown = false;
    try
    {
        await activityStats.GetOverviewAsync(new ActivityStatsQuery
        {
            From = new DateOnly(2026, 10, 20),
            To = new DateOnly(2026, 10, 10)
        });
    }
    catch (ArgumentException)
    {
        badOrderThrown = true;
    }
    Check(badOrderThrown, "PB37: Chặn khoảng thời gian From > To (ném ArgumentException → 400 BadRequest)");

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"ALL {passed} SPRINT 4 REAL-WORLD ACTIVITY & ENTITLEMENT CHECKS PASSED!");
    Console.ResetColor();
}
finally
{
    try { await db.Database.EnsureDeletedAsync(); } catch { }
}

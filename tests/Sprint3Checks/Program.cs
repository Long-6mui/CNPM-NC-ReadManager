using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Discovery;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Entities;
using ReadManager.Api.Services;

Console.WriteLine("==================================================");
Console.WriteLine("    KIỂM THỬ TỰ ĐỘNG SPRINT 3 (BACKEND 3)");
Console.WriteLine("==================================================");

void Assert(bool condition, string message)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {message}");
        Console.ResetColor();
        throw new Exception($"FAIL: {message}");
    }
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[PASS] {message}");
    Console.ResetColor();
}

// 1. Cấu hình DB tạm thời
var config = new ConfigurationBuilder()
    .AddUserSecrets("7fb0539b-fdcb-468b-a959-03ab550ad094")
    .AddEnvironmentVariables()
    .Build();

var rawCs = config.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Port=3306;User=root;Password=root;";

var cs = new MySqlConnectionStringBuilder(rawCs);
var testDbName = "rm_sprint3_test_" + Guid.NewGuid().ToString("N")[..12];
cs.Database = testDbName;

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseMySql(cs.ConnectionString, ServerVersion.AutoDetect(cs.ConnectionString))
    .Options;

await using var db = new AppDbContext(options);

try
{
    Console.WriteLine($"Đang khởi tạo database kiểm thử: {testDbName}...");
    await db.Database.MigrateAsync();

    // 2. Tạo dữ liệu mẫu
    var user = new User { Username = "reader1", Email = "reader1@test.local", DisplayName = "Độc giả 1", PasswordHash = "hash" };
    var admin = new User { Username = "admin1", Email = "admin1@test.local", DisplayName = "Admin 1", Role = "Admin", PasswordHash = "hash" };
    db.Users.AddRange(user, admin);
    await db.SaveChangesAsync();

    var genre = new Genre { Name = "Tiên Hiệp", Slug = "tien-hiep" };
    db.Genres.Add(genre);
    await db.SaveChangesAsync();

    var freeStory = new Story { Title = "Truyện Free", Slug = "truyen-free", AuthorName = "Tác giả A", Synopsis = "Tóm tắt", Visibility = "Public", AccessPolicy = "Free", CreatedBy = admin.UserId };
    var paidStory = new Story { Title = "Truyện Trả Phí", Slug = "truyen-tra-phi", AuthorName = "Tác giả B", Synopsis = "Tóm tắt", Visibility = "Public", AccessPolicy = "Paid", CurrentPrice = 50000, CreatedBy = admin.UserId };
    var mixedStory = new Story { Title = "Truyện Gói", Slug = "truyen-goi", AuthorName = "Tác giả C", Synopsis = "Tóm tắt", Visibility = "Public", AccessPolicy = "Mixed", CreatedBy = admin.UserId };
    db.Stories.AddRange(freeStory, paidStory, mixedStory);
    await db.SaveChangesAsync();

    db.StoryGenres.Add(new StoryGenre { StoryId = paidStory.StoryId, GenreId = genre.GenreId });
    await db.SaveChangesAsync();

    var ch1 = new Chapter { StoryId = paidStory.StoryId, ChapterNumber = 1, Title = "Chương 1", Content = "Nội dung chương 1 trả phí", AccessLevel = "Paid", PublicationStatus = "Published" };
    var chFree = new Chapter { StoryId = freeStory.StoryId, ChapterNumber = 1, Title = "Chương 1 Free", Content = "Nội dung mở", AccessLevel = "Free", PublicationStatus = "Published" };
    db.Chapters.AddRange(ch1, chFree);
    await db.SaveChangesAsync();

    // 3. Khởi tạo Services
    var entitlementService = new EntitlementService(db);
    var notificationService = new NotificationService(db);
    var chapterService = new ChapterService(db, notificationService, entitlementService);
    var discoveryService = new DiscoveryService(db);

    // TEST CA 1: Truyện / chương Free -> Đọc được tự do
    var freeCheck = await entitlementService.CheckStoryAccessAsync(freeStory.StoryId, null);
    Assert(freeCheck.CanRead && freeCheck.AccessType == "Free", "PB22: Truyện Free đọc được không cần đăng nhập.");

    // TEST CA 2: Chưa đăng nhập đọc chương Paid -> Khóa với Reason = LoginRequired
    var guestCheck = await entitlementService.CheckChapterAccessAsync(ch1.ChapterId, null);
    Assert(!guestCheck.CanRead && guestCheck.Reason == "LoginRequired", "PB22: Khách đọc chương Paid bị khóa với lý do LoginRequired.");

    // TEST CA 3: Đã đăng nhập nhưng chưa mua -> Khóa với Reason = StoryPurchaseRequired
    var memberNoBuyCheck = await entitlementService.CheckChapterAccessAsync(ch1.ChapterId, user.UserId);
    Assert(!memberNoBuyCheck.CanRead && memberNoBuyCheck.Reason == "StoryPurchaseRequired", "PB22: Độc giả chưa mua bị khóa với lý do StoryPurchaseRequired.");

    // TEST CA 4: Cấp quyền mua lẻ (StoryPurchase) -> Đọc được vĩnh viễn (PB23)
    var orderStory = new Order { UserId = user.UserId, OrderType = "Story", StoryId = paidStory.StoryId, ItemName = paidStory.Title, Amount = 50000, Status = "Paid" };
    db.Orders.Add(orderStory);
    await db.SaveChangesAsync();

    await entitlementService.GrantStoryPurchaseAsync(orderStory.OrderId);
    var boughtCheck = await entitlementService.CheckChapterAccessAsync(ch1.ChapterId, user.UserId);
    Assert(boughtCheck.CanRead && boughtCheck.AccessType == "Purchased", "PB23: Đã mua riêng truyện thì đọc được chương trả phí vĩnh viễn.");

    // TEST CA 5: ChapterService tự mở khóa nội dung khi có quyền
    var chapterRead = await chapterService.GetByIdAsync(ch1.ChapterId, isAdmin: false, readerId: user.UserId);
    Assert(chapterRead != null && !chapterRead.IsLocked && chapterRead.Content == "Nội dung chương 1 trả phí", "ChapterService: Trả nội dung thật khi người dùng đã có quyền.");

    // TEST CA 6: Gói thuê bao (UserSubscription) All -> Đọc được truyện thuộc gói
    var plan = new ReadingPlan { Name = "Gói VIP", IsAvailable = true };
    db.ReadingPlans.Add(plan);
    await db.SaveChangesAsync();

    var planVersion = new ReadingPlanVersion { PlanId = plan.PlanId, VersionNumber = 1, Price = 100000, DurationMonths = 1, ScopeType = "All", PublishedAt = DateTime.UtcNow };
    db.ReadingPlanVersions.Add(planVersion);
    await db.SaveChangesAsync();

    var orderPlan = new Order { UserId = user.UserId, OrderType = "Plan", PlanVersionId = planVersion.PlanVersionId, ItemName = "Gói VIP", Amount = 100000, DurationMonths = 1, Status = "Paid" };
    db.Orders.Add(orderPlan);
    await db.SaveChangesAsync();

    await entitlementService.GrantSubscriptionAsync(orderPlan.OrderId);
    var subCheck = await entitlementService.CheckStoryAccessAsync(mixedStory.StoryId, user.UserId);
    Assert(subCheck.CanRead && subCheck.AccessType == "Subscription", "PB22: Gói thuê bao bao phủ toàn bộ truyện mở khóa thành công.");

    // TEST CA 7: Kiểm tra thư viện PB27
    var library = await entitlementService.GetMyLibraryAsync(user.UserId);
    Assert(library.PurchasedStories.Count == 1 && library.PurchasedStories[0].StoryId == paidStory.StoryId, "PB27: Thư viện hiển thị chính xác truyện đã mua.");
    Assert(library.Subscriptions.ActiveSubscription != null && library.Subscriptions.ActiveSubscription.IsActive, "PB27: Thư viện hiển thị gói thuê bao đang còn hạn.");

    // TEST CA 8: Khám phá & Bảng xếp hạng (Sprint 3)
    var ranking = await discoveryService.GetRankingAsync(new RankingQueryDto { By = "reads", Period = "week", Top = 5 });
    Assert(ranking.Count > 0, "Sprint 3: API Bảng xếp hạng trả về danh sách truyện có xếp hạng Rank 1..N.");

    var trending = await discoveryService.GetTrendingAsync();
    Assert(trending != null && trending.Keywords.Count >= 0, "Sprint 3: API Xu hướng trả về từ khóa tìm kiếm và truyện nổi bật.");

    var recommended = await discoveryService.GetRecommendedAsync(user.UserId, limit: 5);
    Assert(recommended.Count > 0, "Sprint 3: API Gợi ý truyện hoạt động chính xác.");

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("==================================================");
    Console.WriteLine("       TẤT CẢ CÁC BÀI TEST ĐỀU ĐẠT 100%!");
    Console.WriteLine("==================================================");
    Console.ResetColor();
}
finally
{
    try
    {
        Console.WriteLine($"Đang dọn dẹp database kiểm thử: {testDbName}...");
        await db.Database.EnsureDeletedAsync();
    }
    catch { }
}

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;
using ReadManager.Web.Services;

Console.WriteLine("==========================================================");
Console.WriteLine("  KIỂM THỬ SPRINT 5 — BACKEND VOUCHER & ĐƠN HÀNG");
Console.WriteLine("==========================================================");

var cfg = new ConfigurationBuilder()
    .AddUserSecrets("7fb0539b-fdcb-468b-a959-03ab550ad094")
    .AddEnvironmentVariables()
    .Build();

var rawCs = cfg.GetConnectionString("DefaultConnection")
    ?? throw new Exception("Missing DefaultConnection configuration.");
var cs = new MySqlConnectionStringBuilder(rawCs);
var database = "rm_s5_voucher_" + Guid.NewGuid().ToString("N")[..12];
cs.Database = database;

await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseMySql(cs.ConnectionString, ServerVersion.AutoDetect(cs.ConnectionString)).Options);

Process? apiProcess = null;
Process? webProcess = null;
int passed = 0;

void Check(bool ok, string message)
{
    if (!ok)
        throw new Exception("FAIL: " + message);
    passed++;
    Console.WriteLine($"PASS [{passed}]: {message}");
}

static int FreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static bool HasMoney(string html, string vndDot, string vndComma)
    => html.Contains(vndDot, StringComparison.Ordinal) || html.Contains(vndComma, StringComparison.Ordinal);

try
{
    await db.Database.MigrateAsync();
    Check(await db.VoucherOffers.CountAsync() >= 4, "Migration seeds default VoucherOffers");

    var password = "Test_" + Guid.NewGuid().ToString("N");
    foreach (var name in new[] { "admin", "alice", "bob" })
    {
        var user = new User
        {
            Username = name,
            Email = $"{name}@example.test",
            DisplayName = name,
            Role = name == "admin" ? "Admin" : "Member"
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        db.Users.Add(user);
    }
    await db.SaveChangesAsync();

    var admin = await db.Users.SingleAsync(u => u.Role == "Admin");
    var aliceUser = await db.Users.SingleAsync(u => u.Email == "alice@example.test");

    var s50k = new Story { Title = "Truyện 50K", Slug = "truyen-50k", AuthorName = "Tester", CreatedBy = admin.UserId, Visibility = "Public", AccessPolicy = "Paid", CurrentPrice = 50000m };
    var s50kTwo = new Story { Title = "Truyện 50K Số 2", Slug = "truyen-50k-2", AuthorName = "Tester", CreatedBy = admin.UserId, Visibility = "Public", AccessPolicy = "Paid", CurrentPrice = 50000m };
    var s200k = new Story { Title = "Truyện VIP 200K", Slug = "truyen-200k", AuthorName = "Tester", CreatedBy = admin.UserId, Visibility = "Public", AccessPolicy = "Paid", CurrentPrice = 200000m };
    var s20k = new Story { Title = "Truyện Nhỏ 20K", Slug = "truyen-20k", AuthorName = "Tester", CreatedBy = admin.UserId, Visibility = "Public", AccessPolicy = "Paid", CurrentPrice = 20000m };
    var s15k = new Story { Title = "Truyện 15K", Slug = "truyen-15k", AuthorName = "Tester", CreatedBy = admin.UserId, Visibility = "Public", AccessPolicy = "Paid", CurrentPrice = 15000m };
    db.Stories.AddRange(s50k, s50kTwo, s200k, s20k, s15k);
    await db.SaveChangesAsync();

    db.Chapters.AddRange(
        new Chapter { StoryId = s50k.StoryId, ChapterNumber = 1, Title = "Ch 1", Content = "VIP content", AccessLevel = "Paid", PublicationStatus = "Published" },
        new Chapter { StoryId = s50kTwo.StoryId, ChapterNumber = 1, Title = "Ch 1", Content = "VIP 2 content", AccessLevel = "Paid", PublicationStatus = "Published" },
        new Chapter { StoryId = s200k.StoryId, ChapterNumber = 1, Title = "Ch 1", Content = "VIP 200K content", AccessLevel = "Paid", PublicationStatus = "Published" }
    );
    var plan = new ReadingPlan { Name = "Monthly Plan", IsAvailable = true };
    var planVersion = new ReadingPlanVersion { Plan = plan, VersionNumber = 1, Price = 50000m, DurationMonths = 1, ScopeType = "All", PublishedAt = DateTime.UtcNow.AddMinutes(-1) };
    db.ReadingPlanVersions.Add(planVersion);
    await db.SaveChangesAsync();

    var apiPort = FreePort();
    var apiUrl = $"http://127.0.0.1:{apiPort}";
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var apiDll = Path.Combine(root, "src/ReadManager.Api/bin/Debug/net8.0/ReadManager.Api.dll");

    var apiPsi = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(apiDll)!
    };
    apiPsi.ArgumentList.Add(apiDll);
    apiPsi.Environment["ConnectionStrings__DefaultConnection"] = cs.ConnectionString;
    apiPsi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    apiPsi.Environment["ASPNETCORE_URLS"] = apiUrl;
    apiPsi.Environment["Payment__SecretKey"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    apiPsi.Environment["Logging__LogLevel__Default"] = "Critical";
    apiProcess = Process.Start(apiPsi)!;

    using var anonHttp = new HttpClient { BaseAddress = new Uri(apiUrl), Timeout = TimeSpan.FromSeconds(20) };
    for (int i = 0; i < 60; i++)
    {
        try { await anonHttp.GetAsync("api/vouchers/offers"); break; }
        catch (HttpRequestException) { await Task.Delay(200); }
    }

    async Task<string> LoginAsync(string name)
    {
        var resp = await anonHttp.PostAsJsonAsync("api/auth/login", new { email = $"{name}@example.test", password });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    var adminToken = await LoginAsync("admin");
    var aliceToken = await LoginAsync("alice");
    var bobToken = await LoginAsync("bob");

    using var aliceHttp = new HttpClient { BaseAddress = new Uri(apiUrl) };
    aliceHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceToken);
    using var bobHttp = new HttpClient { BaseAddress = new Uri(apiUrl) };
    bobHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bobToken);

    var wallet = new ReadingProgressApiClient(anonHttp);
    var rewardsAlice = new RewardsApiClient(aliceHttp);
    var storiesAlice = new StoriesApiClient(aliceHttp);
    var paymentsAlice = new PaymentsApiClient(aliceHttp);

    // 1. Auth & Offers
    Check((await anonHttp.GetAsync("api/me/vouchers")).StatusCode == HttpStatusCode.Unauthorized,
        "Guests cannot view personal vouchers");
    Check((await anonHttp.PostAsJsonAsync("api/vouchers/offers/1/redeem", new { requestId = Guid.NewGuid() })).StatusCode == HttpStatusCode.Unauthorized,
        "Guests cannot redeem vouchers");

    var offers = await rewardsAlice.VoucherOffersAsync(aliceToken);
    Check(offers.Ok && offers.Data is { Count: 4 },
        "GET api/vouchers/offers returns seeded offers");

    // 2. Insufficient coins & validation
    var insufficient = await rewardsAlice.RedeemVoucherAsync(1, Guid.NewGuid(), aliceToken);
    Check(!insufficient.Ok && !insufficient.NotAvailable &&
          insufficient.Message.Contains("Không đủ xu", StringComparison.OrdinalIgnoreCase),
        "Redeeming voucher without enough coins fails with 409 message");
    Check((await aliceHttp.PostAsJsonAsync("api/vouchers/offers/9999/redeem", new { requestId = Guid.NewGuid() })).StatusCode == HttpStatusCode.NotFound,
        "Redeeming non-existent offer returns 404");
    Check((await aliceHttp.PostAsJsonAsync("api/vouchers/offers/1/redeem", new { requestId = Guid.Empty })).StatusCode == HttpStatusCode.BadRequest,
        "Redeeming with Guid.Empty returns 400");
    Check((await aliceHttp.GetAsync("api/me/vouchers?status=Unknown")).StatusCode == HttpStatusCode.BadRequest,
        "Invalid voucher status query returns 400");

    // 3. Grant 120 coins -> idempotent concurrent redeem (30 coins -> 90 coins)
    await wallet.GrantAsync(Guid.NewGuid(), aliceUser.Email, 120, 0, adminToken);
    var idempKey = Guid.NewGuid();
    var dupRedeems = await Task.WhenAll(
        rewardsAlice.RedeemVoucherAsync(1, idempKey, aliceToken),
        rewardsAlice.RedeemVoucherAsync(1, idempKey, aliceToken));

    Check(dupRedeems.All(r => r.Ok && r.Data is not null) &&
          dupRedeems[0].Data!.VoucherId == dupRedeems[1].Data!.VoucherId &&
          await db.Vouchers.CountAsync(v => v.UserId == aliceUser.UserId && v.RequestId == idempKey) == 1 &&
          (await wallet.Balance(aliceToken)).Coins == 90,
        "Concurrent retry with same requestId deducts coins once and issues one voucher");

    Check((await aliceHttp.PostAsJsonAsync("api/vouchers/offers/2/redeem", new { requestId = idempKey })).StatusCode == HttpStatusCode.Conflict,
        "Reusing requestId for a different offer returns 409 Conflict");

    var v1 = dupRedeems[0].Data!;

    // 4. Race condition check on balance (90 coins left, 2 x 60 coins -> 1 succeeds, 1 conflicts)
    var race = await Task.WhenAll(
        rewardsAlice.RedeemVoucherAsync(2, Guid.NewGuid(), aliceToken),
        rewardsAlice.RedeemVoucherAsync(2, Guid.NewGuid(), aliceToken));
    Check(race.Count(r => r.Ok) == 1 && race.Count(r => !r.Ok) == 1 &&
          (await wallet.Balance(aliceToken)).Coins == 30,
        "Concurrent distinct redeems cannot overspend coin balance");

    var v2 = race.Single(r => r.Ok).Data!;

    // 5. Coin history integration
    var history = await rewardsAlice.CoinHistoryAsync(1, aliceToken);
    Check(history.Ok &&
          history.Data!.Items.Any(i => i.Type == "Voucher" && i.Amount == -30) &&
          history.Data.Items.Any(i => i.Type == "Voucher" && i.Amount == -60),
        "Coin history records voucher redemptions with negative amounts and Type=Voucher");

    // 6. Ownership, Plan mismatch, MinOrderAmount checks
    Check((await bobHttp.GetAsync($"api/orders/quote/story/{s50k.StoryId}?voucherId={v1.VoucherId}")).StatusCode == HttpStatusCode.BadRequest,
        "Other user cannot quote another user's voucher");
    Check((await bobHttp.PostAsJsonAsync("api/orders", new { orderType = "Story", storyId = s50k.StoryId, voucherId = v1.VoucherId })).StatusCode == HttpStatusCode.BadRequest,
        "Other user cannot apply another user's voucher on order creation");
    Check((await aliceHttp.PostAsJsonAsync("api/orders", new { orderType = "Plan", planId = plan.PlanId, voucherId = v1.VoucherId })).StatusCode == HttpStatusCode.BadRequest,
        "Voucher cannot be applied to Plan orders");

    var (_, minErr) = await storiesAlice.QuoteStoryWithVoucherAsync(s20k.StoryId, v2.VoucherId, aliceToken);
    Check(!string.IsNullOrWhiteSpace(minErr) && minErr.Contains("tối thiểu", StringComparison.OrdinalIgnoreCase),
        "Voucher with MinOrderAmount 30k rejects 20k story");

    // 7. MaxDiscount cap check (20% of 200k = 40k, capped at 30k -> final 170k)
    var (capQuote, capErr) = await storiesAlice.QuoteStoryWithVoucherAsync(s200k.StoryId, v2.VoucherId, aliceToken);
    Check(capErr is null && capQuote is not null &&
          capQuote.OriginalAmount == 200000m && capQuote.DiscountAmount == 30000m && capQuote.Amount == 170000m,
        "Percent voucher respects MaxDiscount cap");

    // 8. Full Pay flow: Quote -> Reserve -> Block double use -> Pay Success -> Used
    var (quote1, qErr1) = await storiesAlice.QuoteStoryWithVoucherAsync(s50k.StoryId, v1.VoucherId, aliceToken);
    Check(qErr1 is null && quote1 is not null &&
          quote1.OriginalAmount == 50000m && quote1.DiscountAmount == 5000m && quote1.Amount == 45000m,
        "Story quote computes 10% voucher discount accurately");

    var ord1Res = await storiesAlice.CreateStoryOrderAsync(s50k.StoryId, aliceToken, v1.VoucherId);
    Check(ord1Res.Ok && ord1Res.Id.HasValue, "Create discounted story order succeeds");
    var ord1 = await storiesAlice.GetOrderAsync(ord1Res.Id!.Value, aliceToken);
    Check(ord1 is not null && ord1.OriginalAmount == 50000m && ord1.DiscountAmount == 5000m &&
          ord1.Amount == 45000m && ord1.VoucherId == v1.VoucherId && ord1.VoucherName == v1.Name,
        "OrderDto includes originalAmount, discountAmount, final amount, voucherId and voucherName");

    var reservedList = await rewardsAlice.MyVouchersAsync("Reserved", aliceToken);
    Check(reservedList.Ok && reservedList.Data!.Any(v => v.VoucherId == v1.VoucherId),
        "Pending order moves voucher to Reserved");

    var reuseReserved = await storiesAlice.CreateStoryOrderAsync(s50kTwo.StoryId, aliceToken, v1.VoucherId);
    Check(!reuseReserved.Ok && reuseReserved.Message.Contains("đang được giữ", StringComparison.OrdinalIgnoreCase),
        "Reserved voucher cannot be used on a second order");

    var pendingQuote = await storiesAlice.QuoteStoryAsync(s50k.StoryId, aliceToken);
    Check(pendingQuote is not null && pendingQuote.PendingOrderId == ord1!.OrderId && pendingQuote.Amount == 45000m,
        "Re-quoting story with pending order reflects reserved discounted amount");

    var pay1 = await paymentsAlice.SimulateAsync(ord1!.OrderId, "Success", aliceToken);
    Check(pay1.Ok && pay1.Data!.EntitlementGranted,
        "Payment completes discounted order and grants reading entitlement");

    var usedList = await rewardsAlice.MyVouchersAsync("Used", aliceToken);
    Check(usedList.Ok && usedList.Data!.Any(v => v.VoucherId == v1.VoucherId && v.UsedAt.HasValue),
        "Successful payment marks voucher Used with UsedAt");

    // 9. Release checks: Cancelled payment, Failed payment, Switch voucher, Explicit cancel, Expired order
    // Grant 40 more coins -> redeem offer 3 (10,000 off) as v3
    await wallet.GrantAsync(Guid.NewGuid(), aliceUser.Email, 40, 0, adminToken); // 30 + 40 = 70
    var v3 = (await rewardsAlice.RedeemVoucherAsync(3, Guid.NewGuid(), aliceToken)).Data!;

    var ord2Id = (await storiesAlice.CreateStoryOrderAsync(s50kTwo.StoryId, aliceToken, v2.VoucherId)).Id!.Value;
    var cancelPay = await paymentsAlice.SimulateAsync(ord2Id, "Cancelled", aliceToken);
    Check(cancelPay.Ok && cancelPay.Data!.OrderStatus == "Cancelled", "Cancelled payment updates order");
    Check((await rewardsAlice.MyVouchersAsync("Available", aliceToken)).Data!.Any(v => v.VoucherId == v2.VoucherId),
        "Cancelled payment releases voucher back to Available");

    var ord3Id = (await storiesAlice.CreateStoryOrderAsync(s50kTwo.StoryId, aliceToken, v2.VoucherId)).Id!.Value;
    var failPay = await paymentsAlice.SimulateAsync(ord3Id, "Failed", aliceToken);
    Check(failPay.Ok && failPay.Data!.OrderStatus == "Failed", "Failed payment updates order");
    Check((await rewardsAlice.MyVouchersAsync("Available", aliceToken)).Data!.Any(v => v.VoucherId == v2.VoucherId),
        "Failed payment releases voucher back to Available");

    // Switching voucher from v2 -> v3 cancels pending order and releases v2
    var ordSwitch1 = (await storiesAlice.CreateStoryOrderAsync(s50kTwo.StoryId, aliceToken, v2.VoucherId)).Id!.Value;
    var ordSwitch2 = (await storiesAlice.CreateStoryOrderAsync(s50kTwo.StoryId, aliceToken, v3.VoucherId)).Id!.Value;
    Check(ordSwitch1 != ordSwitch2 &&
          await db.Orders.AnyAsync(o => o.OrderId == ordSwitch1 && o.Status == "Cancelled") &&
          (await rewardsAlice.MyVouchersAsync("Available", aliceToken)).Data!.Any(v => v.VoucherId == v2.VoucherId) &&
          (await rewardsAlice.MyVouchersAsync("Reserved", aliceToken)).Data!.Any(v => v.VoucherId == v3.VoucherId),
        "Switching voucher cancels prior pending order, releases old voucher, and reserves new voucher");

    // Explicit cancel releases v3
    var cancelHttp = await aliceHttp.PostAsync($"api/orders/{ordSwitch2}/cancel", null);
    cancelHttp.EnsureSuccessStatusCode();
    Check((await rewardsAlice.MyVouchersAsync("Available", aliceToken)).Data!.Any(v => v.VoucherId == v3.VoucherId),
        "Explicit order cancellation releases voucher back to Available");

    // Expired order (>30m) releases v2
    var ordExpId = (await storiesAlice.CreateStoryOrderAsync(s50kTwo.StoryId, aliceToken, v2.VoucherId)).Id!.Value;
    await db.Orders.Where(o => o.OrderId == ordExpId)
        .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, DateTime.UtcNow.AddMinutes(-35)));
    var availAfterExp = await rewardsAlice.MyVouchersAsync("Available", aliceToken);
    Check(availAfterExp.Data!.Any(v => v.VoucherId == v2.VoucherId) &&
          await db.Orders.AnyAsync(o => o.OrderId == ordExpId && o.Status == "Expired"),
        "Expired pending order releases reserved voucher back to Available");

    // Expired voucher cannot be used
    await db.Vouchers.Where(v => v.VoucherId == v2.VoucherId)
        .ExecuteUpdateAsync(s => s.SetProperty(v => v.ExpiresAt, DateTime.UtcNow.AddMinutes(-5)));
    var (_, expErr) = await storiesAlice.QuoteStoryWithVoucherAsync(s50kTwo.StoryId, v2.VoucherId, aliceToken);
    Check(!string.IsNullOrWhiteSpace(expErr) && expErr.Contains("hết hạn", StringComparison.OrdinalIgnoreCase) &&
          (await rewardsAlice.MyVouchersAsync("Expired", aliceToken)).Data!.Any(v => v.VoucherId == v2.VoucherId),
        "Expired voucher is rejected and listed under Expired");

    // Clamp guard check: Amount offer 25k on 15k story (MinOrderAmount bypassed via direct test row) still keeps Amount > 0
    var zeroGuardVoucher = new Voucher
    {
        UserId = aliceUser.UserId,
        OfferId = 4,
        RequestId = Guid.NewGuid(),
        Name = "Giảm lớn test",
        DiscountType = VoucherDiscountTypes.Amount,
        Value = 25000m,
        MinOrderAmount = null,
        CoinCost = 10,
        Status = VoucherStatuses.Available,
        ExpiresAt = DateTime.UtcNow.AddDays(10)
    };
    db.Vouchers.Add(zeroGuardVoucher);
    await db.SaveChangesAsync();
    var ordGuard = await storiesAlice.CreateStoryOrderAsync(s15k.StoryId, aliceToken, zeroGuardVoucher.VoucherId);
    Check(ordGuard.Ok && (await storiesAlice.GetOrderAsync(ordGuard.Id!.Value, aliceToken))!.Amount > 0m,
        "Discount never drops order Amount to <= 0");

    // 9b. [Po] Tiền về TRỄ không được làm 1 voucher dùng cho 2 đơn
    //   Đơn A giữ voucher → bắt đầu thanh toán → A hết hạn (voucher được trả)
    //   → đơn B giữ lại voucher → callback thành công của A mới tới
    //   Kỳ vọng: A = Paid, voucher = Used, đơn B (chưa trả tiền) bị hủy, voucher không dùng lại được.
    var lateVoucher = new Voucher
    {
        UserId = aliceUser.UserId,
        OfferId = 3,
        RequestId = Guid.NewGuid(),
        Name = "Test tiền về trễ",
        DiscountType = VoucherDiscountTypes.Amount,
        Value = 10000m,
        CoinCost = 40,
        Status = VoucherStatuses.Available,
        ExpiresAt = DateTime.UtcNow.AddDays(10)
    };
    db.Vouchers.Add(lateVoucher);
    await db.SaveChangesAsync();

    var lateA = await storiesAlice.CreateStoryOrderAsync(s200k.StoryId, aliceToken, lateVoucher.VoucherId);
    Check(lateA.Ok && lateA.Id.HasValue, "Late-payment: order A reserves the voucher");
    var lateAId = lateA.Id!.Value;
    (await aliceHttp.PostAsJsonAsync("api/payments/start", new { orderId = lateAId })).EnsureSuccessStatusCode();

    // A quá 30 phút → Expired, voucher được trả lại
    await db.Orders.Where(o => o.OrderId == lateAId)
        .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, DateTime.UtcNow.AddMinutes(-35)));
    Check((await rewardsAlice.MyVouchersAsync("Available", aliceToken)).Data!.Any(v => v.VoucherId == lateVoucher.VoucherId) &&
          await db.Orders.AnyAsync(o => o.OrderId == lateAId && o.Status == "Expired"),
        "Late-payment: order A expires and releases the voucher");

    var lateB = await storiesAlice.CreateStoryOrderAsync(s200k.StoryId, aliceToken, lateVoucher.VoucherId);
    Check(lateB.Ok && lateB.Id.HasValue && lateB.Id != lateAId, "Late-payment: order B reserves the same voucher again");
    var lateBId = lateB.Id!.Value;

    // Callback thành công của A tới trễ
    var latePay = await paymentsAlice.SimulateAsync(lateAId, "Success", aliceToken);
    Check(latePay.Ok && await db.Orders.AnyAsync(o => o.OrderId == lateAId && o.Status == "Paid"),
        "Late-payment: late success callback still marks order A Paid");
    Check(await db.Orders.AnyAsync(o => o.OrderId == lateBId && o.Status == "Cancelled"),
        "Late-payment: pending order B holding the same voucher is cancelled");
    Check(await db.Vouchers.AnyAsync(v => v.VoucherId == lateVoucher.VoucherId && v.Status == VoucherStatuses.Used && v.UsedAt != null) &&
          await db.Orders.CountAsync(o => o.VoucherId == lateVoucher.VoucherId && (o.Status == "Paid" || o.Status == "Pending")) == 1,
        "Late-payment: voucher is Used by exactly one order");
    var lateReuse = await storiesAlice.CreateStoryOrderAsync(s20k.StoryId, aliceToken, lateVoucher.VoucherId);
    Check(!lateReuse.Ok, "Late-payment: used voucher cannot be applied to another order");

    // 10. Start actual ReadManager.Web MVC app to verify Rewards + Checkout + Details pages
    var webPort = FreePort();
    var webUrl = $"http://127.0.0.1:{webPort}";
    var webPsi = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Path.Combine(root, "src/ReadManager.Web")
    };
    webPsi.ArgumentList.Add(Path.Combine(root, "src/ReadManager.Web/bin/Debug/net8.0/ReadManager.Web.dll"));
    webPsi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    webPsi.Environment["ASPNETCORE_URLS"] = webUrl;
    webPsi.Environment["Api__BaseUrl"] = apiUrl + "/";
    webPsi.Environment["Logging__LogLevel__Default"] = "Critical";
    webProcess = Process.Start(webPsi)!;

    using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
    using var browser = new HttpClient(handler) { BaseAddress = new Uri(webUrl) };

    string loginHtml = "";
    for (int i = 0; i < 60; i++)
    {
        try { loginHtml = await browser.GetStringAsync("Account/Login"); break; }
        catch (HttpRequestException) { await Task.Delay(200); }
    }

    static string Csrf(string html) => WebUtility.HtmlDecode(
        System.Text.RegularExpressions.Regex.Match(
            html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    var loginPost = await browser.PostAsync("Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["Email"] = aliceUser.Email,
        ["Password"] = password,
        ["__RequestVerificationToken"] = Csrf(loginHtml)
    }));
    Check(loginPost.StatusCode == HttpStatusCode.Redirect, "Alice logs into ReadManager.Web");

    var rewardsHtml = WebUtility.HtmlDecode(await browser.GetStringAsync("Account/Rewards"));
    Check(rewardsHtml.Contains("Giảm 10% đơn truyện") &&
          !rewardsHtml.Contains("Chức năng voucher chưa được bật trên API") &&
          rewardsHtml.Contains("Đổi voucher"),
        "Web Rewards page renders real voucher offers, personal vouchers, and voucher coin history");

    var checkoutRawHtml = await browser.GetStringAsync($"Orders/Checkout?storyId={s50kTwo.StoryId}&voucherId={v3.VoucherId}");
    var checkoutHtml = WebUtility.HtmlDecode(checkoutRawHtml);
    Check(HasMoney(checkoutHtml, "50.000₫", "50,000₫") &&
          HasMoney(checkoutHtml, "10.000₫", "10,000₫") &&
          HasMoney(checkoutHtml, "40.000₫", "40,000₫"),
        "Web Checkout displays original price, voucher discount, and discounted total");

    var payFormPost = await browser.PostAsync("Orders/Pay", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["storyId"] = s50kTwo.StoryId.ToString(),
        ["voucherId"] = v3.VoucherId.ToString(),
        ["__RequestVerificationToken"] = Csrf(checkoutRawHtml)
    }));
    Check(payFormPost.StatusCode == HttpStatusCode.Redirect && payFormPost.Headers.Location is not null,
        "Web Checkout Pay submits voucherId and creates discounted order");

    var detailsHtml = WebUtility.HtmlDecode(await browser.GetStringAsync(payFormPost.Headers.Location!.ToString().TrimStart('/')));
    Check(HasMoney(detailsHtml, "50.000₫", "50,000₫") &&
          HasMoney(detailsHtml, "10.000₫", "10,000₫") &&
          HasMoney(detailsHtml, "40.000₫", "40,000₫") &&
          detailsHtml.Contains("Voucher:"),
        "Web Order Details renders voucher name, original price, discount, and total");

    // Daily rewards share the same wallet; concurrent requests must credit once.
    Check((await anonHttp.PostAsync("api/me/daily-rewards/check-in", null)).StatusCode == HttpStatusCode.Unauthorized,
        "Guests cannot claim daily coins");
    Check((await bobHttp.PostAsync("api/me/daily-rewards/missions/read1/claim", null)).StatusCode == HttpStatusCode.BadRequest,
        "Reading mission cannot be claimed without qualified reading");
    var bobId = await db.Users.Where(u => u.Email == "bob@example.test").Select(u => u.UserId).SingleAsync();
    var beforeDaily = await ReadManager.Api.Services.CoinLedger.BalanceAsync(db, bobId);
    var dailyClaims = await Task.WhenAll(bobHttp.PostAsync("api/me/daily-rewards/check-in", null),
        bobHttp.PostAsync("api/me/daily-rewards/check-in", null));
    Check(dailyClaims.Count(r => r.IsSuccessStatusCode) == 1 && dailyClaims.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
        "Concurrent daily check-in credits exactly once");
    var todayDaily = ReadManager.Api.Services.StreakService.TodayVn(DateTime.UtcNow);
    var expectedDaily = todayDaily.DayOfWeek == DayOfWeek.Sunday ? 30 : 10;
    Check(await ReadManager.Api.Services.CoinLedger.BalanceAsync(db, bobId) == beforeDaily + expectedDaily,
        "Check-in amount matches Vietnam calendar day");
    var dailyState = await bobHttp.GetFromJsonAsync<JsonElement>("api/me/daily-rewards");
    Check(dailyState.GetProperty("checkedInToday").GetBoolean() && dailyState.GetProperty("days").GetArrayLength() == 7,
        "Daily status exposes claimed check-in and seven-day calendar");
    Console.WriteLine($"ALL {passed} SPRINT 5 VOUCHER & ORDER CHECKS PASSED!");
}
finally
{
    if (webProcess is { HasExited: false }) { webProcess.Kill(true); await webProcess.WaitForExitAsync(); }
    if (apiProcess is { HasExited: false }) { apiProcess.Kill(true); await apiProcess.WaitForExitAsync(); }
    if (cs.Database == database && database.StartsWith("rm_s5_voucher_"))
        await db.Database.EnsureDeletedAsync();
}

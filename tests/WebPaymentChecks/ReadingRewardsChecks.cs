using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;
using ReadManager.Api.Services;

internal static class ReadingRewardsChecks
{
    public static async Task MigrateLegacyAsync(AppDbContext db)
    {
        await db.GetService<IMigrator>().MigrateAsync("20261112033154_AddAdminReaderGrants");
        var user = new User { Username = "legacy", Email = "legacy@example.test", DisplayName = "Legacy" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserStreaks.Add(new UserStreak { UserId = user.UserId, CurrentStreak = 3, LongestStreak = 3,
            LastReadDate = StreakService.TodayVn(DateTime.UtcNow), EarnedStreakCoins = 75 });
        db.FreezeCardPurchases.Add(new FreezeCardPurchase { UserId = user.UserId, RequestId = Guid.NewGuid(), Cost = 50 });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        if (await CoinLedger.BalanceAsync(db, user.UserId) != 25 ||
            !await db.QualifiedReadingDays.AnyAsync(d => d.UserId == user.UserId))
            throw new Exception("Legacy balance/streak migration failed");
        Console.WriteLine("PASS: Migration preserves legacy balance and latest reading day");
    }

    public static async Task RunAsync(HttpClient http, AppDbContext db, string adminToken, string password, Action<bool,string> check)
    {
        var registered = await http.PostAsJsonAsync("api/auth/register",
            new { displayName = "Rewards Reader", email = "rewards@example.test", password });
        registered.EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = "rewards@example.test", password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        using var web = new HttpClient { BaseAddress = http.BaseAddress };
        using var mobile = new HttpClient { BaseAddress = http.BaseAddress };
        web.DefaultRequestHeaders.Authorization = mobile.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var user = await db.Users.SingleAsync(u => u.Email == "rewards@example.test");
        var admin = await db.Users.SingleAsync(u => u.Role == "Admin");
        var story = new Story { Title = "Reward test", Slug = "reward-test", AuthorName = "Test",
            CreatedBy = admin.UserId, Visibility = "Public", AccessPolicy = "Free" };
        var chapter = new Chapter { Story = story, ChapterNumber = 1, Title = "Test",
            Content = "Free content", PublicationStatus = "Published", AccessLevel = "Free" };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        (await web.GetAsync($"api/chapters/{chapter.ChapterId}")).EnsureSuccessStatusCode();
        var info = await web.GetFromJsonAsync<JsonElement>("api/me/streak");
        check(info.GetProperty("currentStreak").GetInt32() == 0, "Opening chapter alone does not start streak");
        var path = $"api/reader/chapters/{chapter.ChapterId}";
        var start = await web.PostAsync(path + "/start", null);
        var ticket = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticket").GetString();
        await Task.Delay(TimeSpan.FromSeconds(61));
        var claim = new { ticket, percent = 100 };
        var results = await Task.WhenAll(web.PostAsJsonAsync(path + "/finish", claim), mobile.PostAsJsonAsync(path + "/finish", claim));
        foreach (var result in results) result.EnsureSuccessStatusCode();
        info = await mobile.GetFromJsonAsync<JsonElement>("api/me/streak");
        check(info.GetProperty("currentStreak").GetInt32() == 1 &&
            await CoinLedger.BalanceAsync(db, user.UserId) == 10, "Web-Mobile concurrent completion counts one day and ten coins");

        // Simulate yesterday's existing state without sleeping for a calendar day.
        await db.QualifiedReadingDays.Where(d => d.UserId == user.UserId).ExecuteDeleteAsync();
        var previousDate1 = StreakService.TodayVn(DateTime.UtcNow).AddDays(-1);
        await db.UserStreaks.Where(s => s.UserId == user.UserId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.LastReadDate, previousDate1));
        start = await mobile.PostAsync(path + "/start", null);
        ticket = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticket").GetString();
        await Task.Delay(TimeSpan.FromSeconds(61));
        results = await Task.WhenAll(web.PostAsJsonAsync(path + "/finish", new { ticket, percent = 100 }),
            mobile.PostAsJsonAsync(path + "/finish", new { ticket, percent = 100 }));
        foreach (var result in results) result.EnsureSuccessStatusCode();
        check(await CoinLedger.BalanceAsync(db, user.UserId) == 15 &&
            await db.UserStreaks.AnyAsync(s => s.UserId == user.UserId && s.CurrentStreak == 2),
            "Rereading an old chapter continues streak and awards five daily coins once");

        var client = new ReadManager.Web.Services.ReadingProgressApiClient(http);
        await client.GrantAsync(Guid.NewGuid(), user.Email, 35, 0, adminToken);
        var purchases = await Task.WhenAll(web.PostAsync($"api/reader/freeze-cards/{Guid.NewGuid()}", null),
            mobile.PostAsync($"api/reader/freeze-cards/{Guid.NewGuid()}", null));
        check(purchases.Count(p => p.IsSuccessStatusCode) == 1 &&
            purchases.Count(p => p.StatusCode == HttpStatusCode.Conflict) == 1,
            "Concurrent different checkouts cannot overspend fifty coins");

        var previousDate2 = StreakService.TodayVn(DateTime.UtcNow).AddDays(-2);
        await db.UserStreaks.Where(s => s.UserId == user.UserId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.LastReadDate, previousDate2));
        await Task.WhenAll(web.GetAsync("api/me/streak"), mobile.GetAsync("api/me/streak"));
        check(await db.UserStreaks.AnyAsync(s => s.UserId == user.UserId && s.FreezeCards == 0 && s.CurrentStreak == 2),
            "A missed day consumes one freeze card across both platforms");
        check(await CoinLedger.BalanceAsync(db, user.UserId) == 0, "Using protection grants no extra coins");

        var history = await mobile.GetFromJsonAsync<JsonElement>("api/reader/coins/history?pageSize=2");
        check(history.GetProperty("items").GetArrayLength() == 2 && history.GetProperty("totalCount").GetInt32() == 4,
            "Ledger paginates chapter reward, streak reward, admin grant and purchase");
        var webRewards = new ReadManager.Web.Services.RewardsApiClient(web);
        var webHistory = await webRewards.CoinHistoryAsync(1, token);
        check(webHistory.Ok && webHistory.Data!.Items[0].Type == "FreezeCard" && webHistory.Data.Items[0].Id > 0,
            "New Web rewards page contract matches backend ledger");
        var all = await web.GetFromJsonAsync<JsonElement>("api/reader/coins/history");
        check(all.GetProperty("items").EnumerateArray().Sum(x => x.GetProperty("amount").GetInt32()) == 0 &&
            all.GetProperty("items")[0].GetProperty("balanceAfter").GetInt64() == 0,
            "History reconciles with wallet balance");
        check((await web.GetAsync("api/reader/coins/history?page=0")).StatusCode == HttpStatusCode.BadRequest,
            "History validates pagination");
        check((await http.GetAsync("api/reader/coins/history")).StatusCode == HttpStatusCode.Unauthorized,
            "Guests cannot access coin history");

        var previousDate3 = StreakService.TodayVn(DateTime.UtcNow).AddDays(-3);
        await db.UserStreaks.Where(s => s.UserId == user.UserId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.LastReadDate, previousDate3));
        info = await web.GetFromJsonAsync<JsonElement>("api/me/streak");
        check(info.GetProperty("currentStreak").GetInt32() == 0 && await CoinLedger.BalanceAsync(db, user.UserId) == 0,
            "Expired unprotected streak resets without deleting wallet history");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;

var config = new ConfigurationBuilder().AddUserSecrets("7fb0539b-fdcb-468b-a959-03ab550ad094").AddEnvironmentVariables().Build();
var cs = new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection") ?? throw new Exception("Missing MySQL configuration"));
var database = "rm_paytest_" + Guid.NewGuid().ToString("N");
cs.Database = database;
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySql(cs.ConnectionString, ServerVersion.AutoDetect(cs.ConnectionString)).Options);
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
async Task Reject(object entity, string name, int expectedError)
{
    await using var attempt = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySql(cs.ConnectionString, new MySqlServerVersion(new Version(8, 0, 21))).Options);
    attempt.Add(entity);
    try { await attempt.SaveChangesAsync(); }
    catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysql && mysql.Number == expectedError)
    { Console.WriteLine("PASS: " + name); return; }
    throw new Exception("FAIL: accepted " + name);
}
try
{
    // Exercise upgrade from the current main schema with an existing order.
    var previous = "20261021120612_AddChapterReads";
    await db.GetService<IMigrator>().MigrateAsync(previous);
    var user = new User { Username="db-test", Email="db@test.local", DisplayName="DB test" };
    db.Users.Add(user); await db.SaveChangesAsync();
    var story = new Story { Title="DB test", Slug="db-test", AuthorName="Test", Synopsis="Test", CreatedBy=user.UserId };
    db.Stories.Add(story); await db.SaveChangesAsync();
    var order = new Order { UserId=user.UserId, StoryId=story.StoryId, OrderType="Story", ItemName="Test", Amount=10 };
    db.Orders.Add(order); await db.SaveChangesAsync();
    await db.Database.MigrateAsync();
    Check(await db.Orders.AnyAsync(x=>x.OrderId==order.OrderId), "upgrade preserves existing order");
    Check(!db.Database.HasPendingModelChanges(), "model matches migration");
    var payment = new Payment { OrderId=order.OrderId, Provider="Test", MerchantRequestId="req-1" };
    db.Payments.Add(payment); await db.SaveChangesAsync();
    await Reject(new Payment { OrderId=order.OrderId, Provider="Test", MerchantRequestId="req-1" }, "duplicate request blocked", 1062);
    await Reject(new Payment { OrderId=int.MaxValue, Provider="Test", MerchantRequestId="bad-fk" }, "missing order blocked", 1452);
    await Reject(new Payment { OrderId=order.OrderId, Provider="Test", MerchantRequestId="bad-success", Status="Succeeded" }, "unverified success blocked", 3819);
    await Reject(new Payment { OrderId=order.OrderId, Provider="Test", MerchantRequestId="bad-amount", ReceivedAmount=-1 }, "negative amount blocked", 3819);
    payment.ProviderTransactionId="tx-1"; payment.ReceivedAmount=10; payment.VerifiedAt=DateTime.UtcNow; payment.SucceededAt=DateTime.UtcNow; payment.Status="Succeeded";
    await db.SaveChangesAsync();
    await Reject(new Payment { OrderId=order.OrderId, Provider="Test", MerchantRequestId="req-2", ProviderTransactionId="tx-1" }, "duplicate provider transaction blocked", 1062);
    db.PaymentEvents.Add(new PaymentEvent { PaymentId=payment.PaymentId, ProviderEventId="evt-1", Payload="{}" }); await db.SaveChangesAsync();
    await Reject(new PaymentEvent { PaymentId=payment.PaymentId, ProviderEventId="evt-1", Payload="{}" }, "duplicate event blocked", 1062);
    db.StoryPurchases.Add(new StoryPurchase { OrderId=order.OrderId }); await db.SaveChangesAsync();
    await Reject(new StoryPurchase { OrderId=order.OrderId }, "duplicate story grant blocked", 1062);
    await Reject(new StoryPurchase { OrderId=order.OrderId, GrantedAt=DateTime.UtcNow, ExpiresAt=DateTime.UtcNow.AddDays(-1) }, "invalid purchase expiry blocked", 3819);
    var plan = new ReadingPlan { Name="Test" };
    var version = new ReadingPlanVersion { Plan=plan, VersionNumber=1, Price=10, DurationMonths=1, ScopeType="All" };
    var planOrder = new Order { UserId=user.UserId, OrderType="Plan", PlanVersion=version, ItemName="Test plan", Amount=10, DurationMonths=1 };
    db.Orders.Add(planOrder); await db.SaveChangesAsync();
    await Reject(new UserSubscription { OrderId=planOrder.OrderId, StartsAt=DateTime.UtcNow, EndsAt=DateTime.UtcNow.AddDays(-1) }, "invalid subscription period blocked", 3819);
    db.UserSubscriptions.Add(new UserSubscription { OrderId=planOrder.OrderId, StartsAt=DateTime.UtcNow, EndsAt=DateTime.UtcNow.AddMonths(1) }); await db.SaveChangesAsync();
    await Reject(new UserSubscription { OrderId=planOrder.OrderId, StartsAt=DateTime.UtcNow, EndsAt=DateTime.UtcNow.AddMonths(1) }, "duplicate subscription grant blocked", 1062);
    try { await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Orders WHERE OrderId={order.OrderId}"); throw new Exception("FAIL: deleted financial history"); }
    catch (MySqlException ex) when (ex.Number==1451)
    { Console.WriteLine("PASS: deleting referenced order blocked"); }
    Console.WriteLine("ALL PAYMENT DB CHECKS PASSED");
}
finally
{
    if (cs.Database == database && database.StartsWith("rm_paytest_") && Guid.TryParseExact(database["rm_paytest_".Length..], "N", out _))
        await db.Database.EnsureDeletedAsync();
}



using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;

var config = new ConfigurationBuilder().AddUserSecrets("7fb0539b-fdcb-468b-a959-03ab550ad094").AddEnvironmentVariables().Build();
var cs = new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection") ?? throw new Exception("Configure API User Secrets first."));
var database = "rm_plans_test_" + Guid.NewGuid().ToString("N")[..16];
cs.Database = database;
var options = new DbContextOptionsBuilder<AppDbContext>().UseMySql(cs.ConnectionString, ServerVersion.AutoDetect(cs.ConnectionString)).Options;
await using var db = new AppDbContext(options);
Process? api = null;
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
try
{
    await db.Database.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "model matches migrations; no new migration required");
    var password = "Test_" + Guid.NewGuid().ToString("N");
    foreach(var role in new[]{"Admin","Member"})
    {
        var user=new User {Username=role,Email=role.ToLowerInvariant()+"@example.test",DisplayName=role,Role=role};
        user.PasswordHash=new PasswordHasher<User>().HashPassword(user,password); db.Users.Add(user);
    }
    await db.SaveChangesAsync();
    var admin=await db.Users.SingleAsync(u=>u.Role=="Admin");
    var story=new Story {Title="Test story",Slug="test-story",AuthorName="Test",CreatedBy=admin.UserId,Visibility="Public"};
    db.Stories.Add(story);await db.SaveChangesAsync();
    var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
    var port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
    var url=$"http://127.0.0.1:{port}";
    var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
    var dll=Path.Combine(root,"src/ReadManager.Api/bin/Debug/net8.0/ReadManager.Api.dll");
    var psi=new ProcessStartInfo("dotnet") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(dll)!};
    psi.ArgumentList.Add(dll);
    psi.Environment["ConnectionStrings__DefaultConnection"]=cs.ConnectionString;
    psi.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
    psi.Environment["ASPNETCORE_URLS"]=url;
    psi.Environment["Logging__LogLevel__Default"]="Critical";
    api=Process.Start(psi)!;
    using var http=new HttpClient {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(20)};
    for(int i=0;i<50;i++){try {await http.GetAsync("api/reading-plans");break;}catch(HttpRequestException){await Task.Delay(200);}}
    const string route="api/reading-plans";
    object Valid()=>new {name=" Monthly ",price=50000m,durationMonths=1,scopeType="Selected",storyIds=new[]{story.StoryId}};
    async Task<JsonElement> Json(HttpResponseMessage res){res.EnsureSuccessStatusCode();return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();}
    async Task Login(string role){var res=await Json(await http.PostAsJsonAsync("api/auth/login",new {email=role+"@example.test",password}));http.DefaultRequestHeaders.Authorization=new("Bearer",res.GetProperty("accessToken").GetString());}
    Check((await http.GetAsync(route)).StatusCode==HttpStatusCode.OK,"guest can list public plans");
    Check((await http.PostAsJsonAsync(route,Valid())).StatusCode==HttpStatusCode.Unauthorized,"guest cannot create plan");
    await Login("member");
    Check((await http.PostAsJsonAsync(route,Valid())).StatusCode==HttpStatusCode.Forbidden,"Member cannot create plan");
    Check((await http.GetAsync(route+"/admin")).StatusCode==HttpStatusCode.Forbidden,"Member cannot list admin plans");
    await Login("admin");
    var created=await http.PostAsJsonAsync(route,Valid());
    Check(created.StatusCode==HttpStatusCode.Created,"Admin creates plan and first version");
    var plan=await Json(created);var id=plan.GetProperty("planId").GetInt32();
    Check(plan.GetProperty("name").GetString()=="Monthly" && plan.GetProperty("currentVersion").GetProperty("versionNumber").GetInt32()==1,"name trimmed and version starts at 1");
    Check(await db.PlanVersionStories.CountAsync()==1,"selected story link saved");
    await Login("member");
    var purchasedVersion=plan.GetProperty("currentVersion").GetProperty("planVersionId").GetInt32();
    var purchase=await Json(await http.PostAsJsonAsync("api/orders",new {orderType="Plan",planId=id,planVersionId=purchasedVersion,amount=1}));
    var purchaseId=purchase.GetProperty("orderId").GetInt32();
    Check(purchase.GetProperty("amount").GetDecimal()==50000m && purchase.GetProperty("status").GetString()=="Pending", "plan order uses DB price 50000 and remains pending");
    Check(await db.Orders.AnyAsync(o=>o.OrderId==purchaseId && o.PlanVersionId==purchasedVersion), "plan order persisted in MySQL");
    var repeat=await Json(await http.PostAsJsonAsync("api/orders",new {orderType="Plan",planId=id,planVersionId=purchasedVersion}));
    Check(repeat.GetProperty("orderId").GetInt32()==purchaseId, "repeated sequential plan purchase reuses pending order");
    var history=await Json(await http.GetAsync("api/me/orders"));
    Check(history.GetProperty("items").EnumerateArray().Any(o=>o.GetProperty("orderId").GetInt32()==purchaseId), "my order history includes plan purchase");
    Check(!await db.UserSubscriptions.AnyAsync(), "pending order does not grant subscription");
    await Login("admin");
    foreach(var body in new object[]{
        new {name=" ",price=1,durationMonths=1,scopeType="All",storyIds=Array.Empty<int>()},
        new {name="Bad",price=0,durationMonths=1,scopeType="All",storyIds=Array.Empty<int>()},
        new {name="Bad",price=1.001m,durationMonths=1,scopeType="All",storyIds=Array.Empty<int>()},
        new {name="Bad",price=1,durationMonths=0,scopeType="All",storyIds=Array.Empty<int>()},
        new {name="Bad",price=1,durationMonths=1,scopeType="Unknown",storyIds=Array.Empty<int>()},
        new {name="Bad",price=1,durationMonths=1,scopeType="Selected",storyIds=Array.Empty<int>()},
        new {name="Bad",price=1,durationMonths=1,scopeType="All",storyIds=new[]{story.StoryId}},
        new {name="Bad",price=1,durationMonths=1,scopeType="Selected",storyIds=new[]{story.StoryId,story.StoryId}},
        new {name="Bad",price=1,durationMonths=1,scopeType="Selected",storyIds=new[]{int.MaxValue}},
        new {name="Bad",price=1,durationMonths=1,scopeType="Selected",storyIds=(int[]?)null}})
        Check((await http.PostAsJsonAsync(route,body)).StatusCode==HttpStatusCode.BadRequest,"invalid plan rejected");
    Check(await db.ReadingPlans.CountAsync()==1,"invalid requests leave no partial plans");
    var bodyV2=new {price=75000m,durationMonths=2,scopeType="All",storyIds=Array.Empty<int>()};
    var v2=await Json(await http.PostAsJsonAsync($"{route}/{id}/versions",bodyV2));
    Check(v2.GetProperty("versionNumber").GetInt32()==2,"new pricing creates version 2");
    var old=await db.ReadingPlanVersions.AsNoTracking().Include(v=>v.PlanVersionStories).SingleAsync(v=>v.PlanId==id&&v.VersionNumber==1);
    Check(old.Price==50000m && old.DurationMonths==1 && old.ScopeType=="Selected" && old.PlanVersionStories.Count==1,"old price/duration/scope preserved");
    var parallel=await Task.WhenAll(http.PostAsJsonAsync($"{route}/{id}/versions",bodyV2),http.PostAsJsonAsync($"{route}/{id}/versions",bodyV2));
    Check(parallel.All(x=>x.StatusCode==HttpStatusCode.Created),"concurrent version creation succeeds without collision");
    var nums=await db.ReadingPlanVersions.Where(v=>v.PlanId==id).OrderBy(v=>v.VersionNumber).Select(v=>v.VersionNumber).ToListAsync();
    Check(nums.SequenceEqual(new[]{1,2,3,4}),"concurrent versions allocated sequentially");
    Check((await http.PatchAsJsonAsync($"{route}/{id}/name",new {name="Renamed"})).StatusCode==HttpStatusCode.NoContent,"Admin can rename plan");
    Check((await http.PatchAsJsonAsync($"{route}/{id}/availability",new {})).StatusCode==HttpStatusCode.BadRequest,"availability cannot be omitted");
    Check((await http.PatchAsJsonAsync($"{route}/{id}/availability",new {isAvailable=false})).StatusCode==HttpStatusCode.NoContent,"Admin stops sales");
    Check((await http.GetAsync($"{route}/{id}")).StatusCode==HttpStatusCode.NotFound,"unavailable plan hidden from public detail");
    Check((await Json(await http.GetAsync(route))).GetProperty("totalCount").GetInt32()==0,"unavailable plan hidden from public list");
    Check((await http.GetAsync($"{route}/{id}/admin")).IsSuccessStatusCode,"Admin still sees unavailable plan");
    Check((await Json(await http.GetAsync($"{route}/{id}/versions"))).GetArrayLength()==4,"stopping sales preserves all historical versions");
    await Login("member");
    Check((await http.GetAsync($"{route}/{id}/versions")).StatusCode==HttpStatusCode.Forbidden,"Member cannot view admin version history");
    Check((await http.PostAsJsonAsync($"{route}/{id}/versions",bodyV2)).StatusCode==HttpStatusCode.Forbidden,"Member cannot add version");
    Check((await http.PatchAsJsonAsync($"{route}/{id}/availability",new {isAvailable=true})).StatusCode==HttpStatusCode.Forbidden,"Member cannot resume sales");
    Check((await http.PatchAsJsonAsync($"{route}/{id}/name",new {name="Attack"})).StatusCode==HttpStatusCode.Forbidden,"Member cannot rename plan");
    await Login("admin");
    await http.PatchAsJsonAsync($"{route}/{id}/availability",new {isAvailable=true});
    var current=await Json(await http.GetAsync($"{route}/{id}"));
    Check(current.GetProperty("currentVersion").GetProperty("versionNumber").GetInt32()==4,"resumed plan exposes latest version");
    Check((await Json(await http.GetAsync(route+"?page=2147483647"))).GetProperty("items").GetArrayLength()==0,"large page does not overflow");
    Check((await http.PostAsJsonAsync($"{route}/2147483647/versions",bodyV2)).StatusCode==HttpStatusCode.NotFound,"unknown plan returns 404");
    Console.WriteLine($"ALL {checks} READING PLAN CHECKS PASSED");
}
finally
{
    if(api is {HasExited:false}) {api.Kill(true);await api.WaitForExitAsync();}
    // Only this randomly named test database can be removed.
    if(cs.Database==database && database.StartsWith("rm_plans_test_") && database.Length==30)
        await db.Database.EnsureDeletedAsync();
}

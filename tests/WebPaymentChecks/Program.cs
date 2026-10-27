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
using ReadManager.Web.Services;

var cfg = new ConfigurationBuilder().AddUserSecrets("7fb0539b-fdcb-468b-a959-03ab550ad094").AddEnvironmentVariables().Build();
var cs = new MySqlConnectionStringBuilder(cfg.GetConnectionString("DefaultConnection") ?? throw new Exception("Missing DB configuration"));
var database = "rm_webpay_" + Guid.NewGuid().ToString("N")[..16]; cs.Database = database;
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySql(cs.ConnectionString,ServerVersion.AutoDetect(cs.ConnectionString)).Options);
Process? process=null;
Process? webProcess=null;
int count=0;
void Check(bool ok,string message) { if(!ok)throw new Exception("FAIL: "+message); Console.WriteLine("PASS: "+message);count++; }
try
{
    await db.Database.MigrateAsync();
    var password="Test_"+Guid.NewGuid().ToString("N");
    foreach(var name in new[]{"admin","alice","bob"})
    {
        var u=new User { Username=name,Email=name+"@example.test",DisplayName=name,Role=name=="admin"?"Admin":"Member" };
        u.PasswordHash=new PasswordHasher<User>().HashPassword(u,password);db.Users.Add(u);
    }
    await db.SaveChangesAsync();
    var admin=await db.Users.SingleAsync(x=>x.Role=="Admin");
    var story=new Story {Title="Paid",Slug="paid",AuthorName="Test",CreatedBy=admin.UserId,Visibility="Public",AccessPolicy="Paid",CurrentPrice=50000};
    var other=new Story {Title="Outside",Slug="outside",AuthorName="Test",CreatedBy=admin.UserId,Visibility="Public",AccessPolicy="Paid",CurrentPrice=50000};
    db.Stories.AddRange(story,other);await db.SaveChangesAsync();
    var chapter=new Chapter {StoryId=story.StoryId,ChapterNumber=1,Title="Paid",Content="Protected text",AccessLevel="Paid",PublicationStatus="Published"};
    var outside=new Chapter {StoryId=other.StoryId,ChapterNumber=1,Title="Paid",Content="Other text",AccessLevel="Paid",PublicationStatus="Published"};
    db.Chapters.AddRange(chapter,outside);
    var plan=new ReadingPlan {Name="Selected",IsAvailable=true};
    var version=new ReadingPlanVersion {Plan=plan,VersionNumber=1,Price=50000,DurationMonths=1,ScopeType="Selected",PublishedAt=DateTime.UtcNow.AddMinutes(-1)};
    version.PlanVersionStories.Add(new PlanVersionStory {StoryId=story.StoryId});db.ReadingPlanVersions.Add(version);await db.SaveChangesAsync();
    var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();var port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
    var url=$"http://127.0.0.1:{port}";
    var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
    var dll=Path.Combine(root,"src/ReadManager.Api/bin/Debug/net8.0/ReadManager.Api.dll");
    var psi=new ProcessStartInfo("dotnet") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(dll)!};psi.ArgumentList.Add(dll);
    psi.Environment["ConnectionStrings__DefaultConnection"]=cs.ConnectionString;
    psi.Environment["ASPNETCORE_ENVIRONMENT"]="Development";psi.Environment["ASPNETCORE_URLS"]=url;
    psi.Environment["Payment__SecretKey"]=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");psi.Environment["Logging__LogLevel__Default"]="Critical";
    process=Process.Start(psi)!;
    using var http=new HttpClient {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(20)};
    for(int i=0;i<60;i++){try{await http.GetAsync("api/reading-plans");break;}catch(HttpRequestException){await Task.Delay(200);}}
    async Task<string> Token(string name) {var response=await http.PostAsJsonAsync("api/auth/login",new {email=name+"@example.test",password});response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;}
    var alice=await Token("alice");var bob=await Token("bob");var adminToken=await Token("admin");
    var webOrders=new StoriesApiClient(http);var payments=new PaymentsApiClient(http);
    var created=await webOrders.CreatePlanOrderAsync(plan.PlanId,version.PlanVersionId,alice);
    Check(created.Ok && created.Id.HasValue,"Web client creates plan order via API");var orderId=created.Id!.Value;
    Check(!(await payments.StartAsync(orderId,bob)).Ok,"other member cannot start payment");
    Check(!(await payments.SimulateAsync(orderId,"Success",bob)).Ok,"other member cannot simulate payment");
    using var mobileHttp = new HttpClient { BaseAddress = new Uri(url + "/api/") };
    var mobileToken = new TestTokenStore(alice);
    var mobileApi = new ReadManager.Mobile.Services.ApiClient(mobileHttp, mobileToken);
    var mobilePayments = new ReadManager.Mobile.Services.ApiPaymentService(mobileApi);
    var mobileStart = await mobilePayments.StartAsync(orderId);
    Check(mobileStart.OrderId == orderId && mobileStart.Amount == 50000 && mobileStart.Currency == "VND", "Mobile payment route/DTO match actual API");
    var start=await payments.StartAsync(orderId,alice);
    Check(start.Ok && start.Data!.Amount==50000,"payment starts with database amount");
    var before=await webOrders.GetChapterAsync(story.StoryId,1,alice);
    Check(before!.IsLocked && before.Content=="","pending payment does not unlock chapter");
    var paid=await mobilePayments.SimulateAsync(orderId,"Success");
    Check(paid.EntitlementGranted,"simulation uses signed callback and grants entitlement");
    var after=await webOrders.GetChapterAsync(story.StoryId,1,alice);
    Check(!after!.IsLocked && after.Content=="Protected text","paid member reads chapter via Web API client");
    Check((await webOrders.GetChapterAsync(other.StoryId,1,alice))!.IsLocked,"selected plan does not unlock outside story");
    Check((await webOrders.GetChapterAsync(story.StoryId,1,bob))!.IsLocked,"other member stays locked");
    var library=await payments.LibraryAsync(alice);
    Check(library.Ok && library.Data!.Subscriptions.History.Any(x=>x.OrderId==orderId && x.IsActive),"Web reads granted subscription from API");
    var mobileAccess = new ReadManager.Mobile.Services.ApiReadingAccessService(mobileApi);
    Check((await mobileAccess.GetPurchasesAsync()).Plans.Any(p => p.PlanId == plan.PlanId), "Mobile library maps active subscription and EndsAt");
    var local = new ReadManager.Mobile.Services.LocalReadingProgressService(new TestLibrary());
    var mobileProgress = new ReadManager.Mobile.Services.ApiReadingProgressService(mobileApi, local, mobileToken);
    Check((await mobileProgress.GetReadingAsync()).Count == 0, "Empty server progress does not resurrect local history");
    await mobileProgress.SaveAsync(story.StoryId, chapter.ChapterId, 1, chapter.Title, 63.5m);
    Check((await mobileProgress.GetProgressAsync(story.StoryId))?.ScrollPercent == 63.5m, "Mobile saves and restores scroll percentage via API");
    var reading = await mobileProgress.GetReadingAsync();
    Check(reading.Count == 1 && reading[0].Title == story.Title && reading[0].LastChapterId == chapter.ChapterId && reading[0].ScrollPercent == 63.5m,
        "Mobile maps paged reading response and lastReadAt");
    var webProgress = new ReadingProgressApiClient(http);
    Check((await webProgress.GetAsync(story.StoryId, alice))?.ScrollPercent == 63.5m,
        "Web sees progress saved on Mobile");
    await webProgress.SaveAsync(story.StoryId, chapter.ChapterId, 42.25m, alice);
    Check((await mobileProgress.GetProgressAsync(story.StoryId))?.ScrollPercent == 42.25m,
        "Mobile sees progress saved on Web");

    // Run the actual MVC app to verify cookie auth, antiforgery and rendered reading/library pages.
    var webListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);
    webListener.Start(); var webPort=((System.Net.IPEndPoint)webListener.LocalEndpoint).Port; webListener.Stop();
    var webUrl=$"http://127.0.0.1:{webPort}";
    var webPsi=new ProcessStartInfo("dotnet") { UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=Path.Combine(root,"src/ReadManager.Web") };
    webPsi.ArgumentList.Add(Path.Combine(root,"src/ReadManager.Web/bin/Debug/net8.0/ReadManager.Web.dll"));
    webPsi.Environment["ASPNETCORE_ENVIRONMENT"]="Development"; webPsi.Environment["ASPNETCORE_URLS"]=webUrl;
    webPsi.Environment["Api__BaseUrl"]=url+"/"; webPsi.Environment["Logging__LogLevel__Default"]="Critical";
    webProcess=Process.Start(webPsi)!;
    using var webHandler=new HttpClientHandler { CookieContainer=new CookieContainer(), AllowAutoRedirect=false };
    using var browser=new HttpClient(webHandler) { BaseAddress=new Uri(webUrl) };
    string loginHtml="";
    for(var n=0;n<60;n++) { try {loginHtml=await browser.GetStringAsync("Account/Login"); break;} catch(HttpRequestException) {await Task.Delay(200);} }
    string Csrf(string html) => WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    var loginForm=new Dictionary<string,string> { ["Email"]="alice@example.test", ["Password"]=password, ["__RequestVerificationToken"]=Csrf(loginHtml) };
    var loginResponse=await browser.PostAsync("Account/Login",new FormUrlEncodedContent(loginForm));
    Check(loginResponse.StatusCode==HttpStatusCode.Redirect,"Web test user signs in with cookie");
    var readHtml=await browser.GetStringAsync($"Stories/Read?storyId={story.StoryId}&no=1");
    Check(readHtml.Contains("data-percent=\"42.25\"") && readHtml.Contains("reading-progress.js"),
        "Web reader renders saved scroll percent from API");
    Check((await browser.GetStringAsync("Library")).Contains("42%"),"Web library renders actual API progress");
    var saveForm=new Dictionary<string,string> { ["storyId"]=story.StoryId.ToString(), ["chapterId"]=chapter.ChapterId.ToString(), ["scrollPercent"]="71.5", ["__RequestVerificationToken"]=Csrf(readHtml) };
    var savedHttp=await browser.PostAsync("Library/SaveProgress",new FormUrlEncodedContent(saveForm));
    Check(savedHttp.IsSuccessStatusCode && (await mobileProgress.GetProgressAsync(story.StoryId))?.ScrollPercent==71.5m,
        "Web MVC save updates Mobile through shared database");
    var badForm=new Dictionary<string,string>(saveForm) { ["scrollPercent"]="101" };
    Check((await browser.PostAsync("Library/SaveProgress",new FormUrlEncodedContent(badForm))).StatusCode==HttpStatusCode.BadRequest,
        "Web rejects invalid scroll percent");
    badForm=new Dictionary<string,string>(saveForm); badForm.Remove("__RequestVerificationToken");
    Check((await browser.PostAsync("Library/SaveProgress",new FormUrlEncodedContent(badForm))).StatusCode==HttpStatusCode.BadRequest,
        "Web save requires antiforgery token");
    using var guest=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false}) {BaseAddress=new Uri(webUrl)};
    Check((await guest.PostAsync("Library/SaveProgress",new FormUrlEncodedContent(saveForm))).StatusCode==HttpStatusCode.Redirect,
        "Guest cannot write progress through Web");
    var lockedHtml=await browser.GetStringAsync($"Stories/Read?storyId={other.StoryId}&no=1");
    Check(!lockedHtml.Contains("id=\"readingProgress\""),"Locked chapter does not render progress writer");
    var removeHttp=await browser.PostAsync("Library/RemoveProgress",new FormUrlEncodedContent(saveForm));
    Check(removeHttp.StatusCode==HttpStatusCode.Redirect && (await mobileProgress.GetReadingAsync()).Count==0,
        "Web removal is reflected on Mobile");
    await mobileProgress.SaveAsync(story.StoryId,chapter.ChapterId,1,chapter.Title,63.5m);
    await mobileProgress.RemoveAsync(story.StoryId);
    Check((await mobileProgress.GetReadingAsync()).Count == 0, "Mobile removal deletes server progress");
    await mobileProgress.SaveAsync(story.StoryId, chapter.ChapterId, 1, chapter.Title, 63.5m);
    var aliceId = (await db.Users.SingleAsync(u => u.Username == "alice")).UserId;
    for (var n=0; n<50; n++)
    {
        var pagedStory = new Story { Title=$"Paged {n}", Slug=$"paged-{n}", AuthorName="Test", CreatedBy=admin.UserId, Visibility="Public", AccessPolicy="Free" };
        var pagedChapter = new Chapter { Story=pagedStory, ChapterNumber=1, Title="Page", Content="Text", PublicationStatus="Published", AccessLevel="Free" };
        db.Chapters.Add(pagedChapter);
        await db.SaveChangesAsync();
        db.ReadingProgresses.Add(new ReadManager.Api.Entities.ReadingProgress { UserId=aliceId, StoryId=pagedStory.StoryId, ChapterId=pagedChapter.ChapterId, ScrollPercent=10, UpdatedAt=DateTime.UtcNow });
    }
    await db.SaveChangesAsync();
    Check((await webProgress.ListAsync(alice)).Count == 51, "Web includes all pages of server progress");
    Check((await mobileProgress.GetReadingAsync()).Count == 51, "Mobile reads all pages rather than losing progress after 50 stories");
    await mobileToken.SaveTokenAsync(bob);
    Check((await mobileAccess.GetPurchasesAsync()).Plans.Count == 0 && (await mobileProgress.GetReadingAsync()).Count == 0, "Switch account does not expose previous account rights/progress");
    await mobileToken.SaveTokenAsync("invalid");
    var rejected = false;
    try { await mobileProgress.GetReadingAsync(); } catch (ReadManager.Mobile.Services.ApiException ex) when (ex.StatusCode == 401) { rejected = true; }
    Check(rejected, "Expired session surfaces error rather than stale progress");
    await mobileToken.SaveTokenAsync(alice);
    var duplicate=await payments.SimulateAsync(orderId,"Success",alice);
    Check(await db.UserSubscriptions.CountAsync(x=>x.OrderId==orderId)==1,"repeat confirmation does not grant twice");
    await db.UserSubscriptions.Where(x=>x.OrderId==orderId).ExecuteDeleteAsync();
    Check((await mobileAccess.GetPurchasesAsync()).Plans.Count == 0, "Mobile does not infer rights from Paid order when grant is missing");
    Check(!(await payments.RecoveryAsync(1,alice)).Ok,"Member cannot inspect recovery queue");
    var recoveries=await payments.RecoveryAsync(1,adminToken);
    Check(recoveries.Ok && recoveries.Data!.Items.Any(x=>x.OrderId==orderId),"Admin sees verified payment missing grant");
    Check((await payments.RegrantAsync(orderId,adminToken)).Data!.Granted,"Admin regrants through API");
    Check((await payments.RegrantAsync(orderId,adminToken)).Data!.AlreadyGranted,"regrant is idempotent");
    Check((await db.Payments.CountAsync(x=>x.OrderId==orderId))==1,"recovery creates no extra payment");
    Console.WriteLine($"ALL {count} WEB PAYMENT CHECKS PASSED");
}
finally
{
    if(webProcess is {HasExited:false}) {webProcess.Kill(true);await webProcess.WaitForExitAsync();}
    if(process is {HasExited:false}) {process.Kill(true);await process.WaitForExitAsync();}
    if(cs.Database==database && database.StartsWith("rm_webpay_") && database.Length==26) await db.Database.EnsureDeletedAsync();
}

sealed class TestTokenStore(string token) : ReadManager.Mobile.Services.ITokenStore
{
    public Task<string?> GetTokenAsync() => Task.FromResult<string?>(token);
    public Task SaveTokenAsync(string value) { token=value; return Task.CompletedTask; }
    public Task ClearAsync() { token=""; return Task.CompletedTask; }
}
sealed class TestLibrary : ReadManager.Mobile.Services.ILibraryService
{
    private readonly List<ReadManager.Mobile.Models.LibraryEntry> entries = new() { new() { StoryId=9999, LastChapterId=9999, Title="Stale local" } };
    public IReadOnlyList<ReadManager.Mobile.Models.LibraryEntry> GetHistory() => entries;
    public IReadOnlyList<ReadManager.Mobile.Models.LibraryEntry> GetFavorites() => new List<ReadManager.Mobile.Models.LibraryEntry>();
    public void AddToHistory(ReadManager.Mobile.Models.StoryDetail story) { }
    public void RecordChapter(int storyId, int chapterId, int chapterNumber, string title, decimal scrollPercent=0) { }
    public void RemoveFromHistory(int id) { }
    public void ClearHistory() { }
    public bool IsFavorite(int id) => false;
    public bool ToggleFavorite(ReadManager.Mobile.Models.StoryDetail story) => false;
    public void RemoveFavorite(int id) { }
}

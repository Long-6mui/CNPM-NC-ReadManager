using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Auth;
using ReadManager.Api.DTOs.Chapters;
using ReadManager.Api.Entities;
using ReadManager.Api.Services;

var config = new ConfigurationBuilder().AddUserSecrets("7fb0539b-fdcb-468b-a959-03ab550ad094").AddEnvironmentVariables().Build();
var cs = new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection") ?? throw new Exception("Missing local MySQL configuration."));
var database = "readmanager_test_" + Guid.NewGuid().ToString("N")[..16];
cs.Database = database;
var options = new DbContextOptionsBuilder<AppDbContext>().UseMySql(cs.ConnectionString, ServerVersion.AutoDetect(cs.ConnectionString)).Options;
await using var db = new AppDbContext(options);
Process? api = null;
void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
IFormFile Txt(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes),0,bytes.Length,"Files",name);
try
{
    // Only this uniquely named disposable database is created or deleted.
    await db.Database.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "MySQL model matches migrations");
    var auth = new AuthService(db, new EphemeralDataProtectionProvider());
    var password = "Test_" + Guid.NewGuid().ToString("N");
    var registered = await auth.RegisterAsync(new RegisterRequestDto { Email="member@example.test",DisplayName="Member",Password=password });
    Check(registered.Success && registered.User!.Role == "Member", "register creates active Member");
    Check(!(await auth.RegisterAsync(new RegisterRequestDto {Email="member@example.test",DisplayName="Again",Password=password})).Success,"duplicate email rejected");
    var login = await auth.LoginAsync(new LoginRequestDto { Email="member@example.test",Password=password });
    Check(login.Success && !string.IsNullOrEmpty(login.Data!.AccessToken),"new Member can log in with registered password");
    Check(!(await auth.LoginAsync(new LoginRequestDto {Email="member@example.test",Password="wrong"})).Success,"wrong password rejected");
    var member = await db.Users.SingleAsync();
    var admin = new User {Username="admin-test",Email="admin@example.test",DisplayName="Admin",Role="Admin",AccountStatus="Active"};
    admin.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(admin,password);
    db.Users.Add(admin); await db.SaveChangesAsync();
    var story = new Story {Title="Test",Slug="test",AuthorName="Author",Synopsis="Synopsis",Visibility="Public",AccessPolicy="Mixed",CurrentPrice=10,CreatedBy=admin.UserId};
    db.Stories.Add(story); await db.SaveChangesAsync();
    var chapters = new ChapterService(db);
    var free = await chapters.CreateAsync(story.StoryId,new CreateChapterDto {ChapterNumber=1,Title="Free",Content="Free content",PublicationStatus="Published"});
    var paid = await chapters.CreateAsync(story.StoryId,new CreateChapterDto {ChapterNumber=2,Title="Paid",Content="Secret content",AccessLevel="Paid",PublicationStatus="Published"});
    var draft = await chapters.CreateAsync(story.StoryId,new CreateChapterDto {ChapterNumber=3,Title="Draft",Content="Draft content"});
    Check((await chapters.GetListAsync(story.StoryId,false))!.Count==2,"public list excludes draft");
    Check((await chapters.GetByIdAsync(free!.ChapterId,false))!.Content=="Free content","free chapter readable");
    var locked=await chapters.GetByIdAsync(paid!.ChapterId,false);
    Check(locked!.IsLocked && locked.Content=="","paid chapter does not leak content");
    Check((await chapters.GetByIdAsync(paid.ChapterId,true))!.Content=="Secret content","Admin can preview paid chapter");
    Check(await chapters.GetByIdAsync(draft!.ChapterId,false)==null,"draft cannot be read by guest");
    story.Visibility="Hidden";await db.SaveChangesAsync();
    Check(await chapters.GetByIdAsync(free.ChapterId,false)==null,"hidden story cannot be read");
    story.Visibility="Public";await db.SaveChangesAsync();
    var duplicate=false;try {await chapters.CreateAsync(story.StoryId,new CreateChapterDto {ChapterNumber=1,Title="dup",Content="dup"});}catch(InvalidOperationException){duplicate=true;}
    Check(duplicate,"duplicate chapter number rejected");
    await chapters.UpdateAsync(draft.ChapterId,new UpdateChapterDto {ChapterNumber=4,Title="Updated",Content="Updated content",PublicationStatus="Published"});
    Check((await chapters.GetByNumberAsync(story.StoryId,4,false))!.Title=="Updated","chapter renumber and edit persisted");
    var text="Chương 5: Năm\nNội dung năm\nChương 6: Sáu\nNội dung sáu";
    var preview=await chapters.UploadChapterFilesAsync(story.StoryId,new UploadChapterFilesDto {Text=text});
    Check(preview!.CanSave && !preview.Saved && await db.Chapters.CountAsync()==3,"upload preview does not save");
    var saved=await chapters.UploadChapterFilesAsync(story.StoryId,new UploadChapterFilesDto {Text=text,Save=true});
    Check(saved!.Saved && saved.Created==2,"upload saves parsed chapters");
    var count=await db.Chapters.CountAsync();
    var invalid=await chapters.UploadChapterFilesAsync(story.StoryId,new UploadChapterFilesDto {Text="Chương 7: A\nA\nChương 7: B\nB",Save=true});
    Check(!invalid!.Saved && await db.Chapters.CountAsync()==count,"invalid upload writes nothing");
    var tooMany=await ChapterFileReader.ReadAsync(string.Join("\n",Enumerable.Range(1,501).Select(n=>$"Chương {n}: Test\nContent")),[]);
    Check(tooMany.Errors.Count>0,"501 chapters rejected early");
    using var zipBytes=new MemoryStream();
    using(var zip=new ZipArchive(zipBytes,ZipArchiveMode.Create,true))
        for(int i=1;i<=21;i++){using var writer=new StreamWriter(zip.CreateEntry($"{i}.txt").Open());writer.Write(new string('a',1024*1024));}
    var zipped=await ChapterFileReader.ReadAsync(null,[Txt("large.zip",zipBytes.ToArray())]);
    Check(zipped.Errors.Any(e=>e.Contains("20MB")),"ZIP aggregate expansion over 20MB rejected");
    var multi=await ChapterFileReader.ReadAsync(null,Enumerable.Range(1,3).Select(i=>Txt($"{i}.txt",new byte[8*1024*1024])).ToArray());
    Check(multi.Errors.Any(e=>e.Contains("20MB")),"budget shared across files");

    var repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
    var dll=Path.Combine(repo,"src/ReadManager.Api/bin/Debug/net8.0/ReadManager.Api.dll");
    var psi=new ProcessStartInfo("dotnet") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(dll)!};
    psi.ArgumentList.Add(dll);
    psi.Environment["ConnectionStrings__DefaultConnection"]=cs.ConnectionString;
    psi.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
    psi.Environment["ASPNETCORE_URLS"]="http://127.0.0.1:5297";
    psi.Environment["Logging__LogLevel__Default"]="Error";
    api=Process.Start(psi)!;
    using var client=new HttpClient {BaseAddress=new Uri("http://127.0.0.1:5297"),Timeout=TimeSpan.FromSeconds(10)};
    for(int i=0;i<30;i++){try{await client.GetAsync("api/genres");break;}catch(HttpRequestException){await Task.Delay(300);}}
    Check((await client.PostAsJsonAsync("api/stories",new {})).StatusCode==HttpStatusCode.Unauthorized,"guest cannot create stories");
    async Task<string> Token(string email){var response=await client.PostAsJsonAsync("api/auth/login",new {email,password});response.EnsureSuccessStatusCode();return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;}
    var memberToken=await Token(member.Email);
    client.DefaultRequestHeaders.Authorization=new("Bearer",memberToken);
    Check((await client.PostAsJsonAsync($"api/stories/{story.StoryId}/chapters",new {})).StatusCode==HttpStatusCode.Forbidden,"Member cannot create chapters");
    Check((await client.PutAsJsonAsync($"api/stories/{story.StoryId}",new {})).StatusCode==HttpStatusCode.Forbidden,"Member cannot change story access policy");
    var logout=await client.PostAsJsonAsync("api/auth/logout",new {});logout.EnsureSuccessStatusCode();
    Check((await client.GetAsync("api/auth/me")).StatusCode==HttpStatusCode.Unauthorized,"logout revokes API session");
    client.DefaultRequestHeaders.Authorization=new("Bearer",await Token(admin.Email));
    Check((await client.GetAsync("api/stories/admin")).IsSuccessStatusCode,"Admin story list available");
    var created=await client.PostAsJsonAsync($"api/stories/{story.StoryId}/chapters",new {chapterNumber=10,title="HTTP",content="HTTP content",accessLevel="Free",publicationStatus="Published"});
    Check(created.StatusCode==HttpStatusCode.Created,"Admin creates chapter through HTTP API");
    using var mobileHttp = new HttpClient { BaseAddress=new Uri("http://127.0.0.1:5297/api/") };
    var tokenStore = new MemoryTokenStore();
    var mobileClient = new ReadManager.Mobile.Services.ApiClient(mobileHttp, tokenStore);
    var mobileAuth = new ReadManager.Mobile.Services.Auth.AuthService(mobileClient, tokenStore);
    var mobileUser = await mobileAuth.RegisterAsync(new() { Email="mobile@example.test", DisplayName="Mobile", Password=password });
    Check(mobileUser.Role=="Member" && await tokenStore.GetTokenAsync()==null, "real mobile service parses registration without inventing token");
    await mobileAuth.LoginAsync(new() {UsernameOrEmail=mobileUser.Email,Password=password});
    Check(await mobileAuth.IsLoggedInAsync() && mobileAuth.CurrentUser?.Email==mobileUser.Email, "real mobile service logs in and restores profile");
    var mobileToken=await tokenStore.GetTokenAsync();
    await mobileAuth.LogoutAsync();
    Check(await tokenStore.GetTokenAsync()==null && mobileAuth.CurrentUser==null, "mobile logout clears local session");
    client.DefaultRequestHeaders.Authorization=new("Bearer",mobileToken);
    Check((await client.GetAsync("api/auth/me")).StatusCode==HttpStatusCode.Unauthorized,"mobile logout revokes server session");
    bool validationShown=false;
    try { await mobileAuth.RegisterAsync(new() {Email="invalid",Password="x"}); }
    catch(ReadManager.Mobile.Services.ApiException ex) {validationShown=ex.StatusCode==400 && !string.IsNullOrWhiteSpace(ex.Message);}
    Check(validationShown,"mobile displays API validation error");
    Console.WriteLine("ALL SPRINT 1 CHECKS PASSED");
}
finally
{
    if(api is {HasExited:false}){api.Kill(true);await api.WaitForExitAsync();}
    if (database.StartsWith("readmanager_test_") && database.Length==32 && cs.Database==database)
        await db.Database.EnsureDeletedAsync();
}


sealed class MemoryTokenStore : ReadManager.Mobile.Services.ITokenStore
{
    private string? token;
    public Task<string?> GetTokenAsync()=>Task.FromResult(token);
    public Task SaveTokenAsync(string value){token=value;return Task.CompletedTask;}
    public Task ClearAsync(){token=null;return Task.CompletedTask;}
}

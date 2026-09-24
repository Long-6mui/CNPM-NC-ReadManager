using Microsoft.AspNetCore.Authentication.Cookies;
using ReadManager.Web.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<AuthApiClient>(client=>{client.BaseAddress=new Uri(builder.Configuration["Api:BaseUrl"]??"https://localhost:7188/");client.Timeout=TimeSpan.FromSeconds(10);});
builder.Services.AddScoped<ApiCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options=>{
 options.Cookie.Name="ReadManager.Web.Session";options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Lax;options.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
 options.LoginPath="/Account/Login";options.AccessDeniedPath="/Account/AccessDenied";options.ExpireTimeSpan=TimeSpan.FromHours(8);options.SlidingExpiration=false;options.EventsType=typeof(ApiCookieEvents);
});
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Stories}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

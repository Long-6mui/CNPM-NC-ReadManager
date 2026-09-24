using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Authentication;
using ReadManager.Api.Data;
using System.Threading.RateLimiting;
var builder=WebApplication.CreateBuilder(args);
var connectionString=builder.Configuration.GetConnectionString("DefaultConnection")??throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:DefaultConnection.");
builder.Services.AddDbContext<AppDbContext>(options=>options.UseMySql(connectionString,ServerVersion.AutoDetect(connectionString)));
builder.Services.AddDataProtection().SetApplicationName("ReadManager.Api");
builder.Services.AddAuthentication(ApiSessionHandler.SchemeName).AddScheme<AuthenticationSchemeOptions,ApiSessionHandler>(ApiSessionHandler.SchemeName,_=>{});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options=>{options.RejectionStatusCode=429;options.AddPolicy("login",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions {PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddControllers();builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();
var app=builder.Build();
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.UseHttpsRedirection();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapControllers();app.Run();

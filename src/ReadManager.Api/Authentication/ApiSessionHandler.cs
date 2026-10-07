using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReadManager.Api.Data;
namespace ReadManager.Api.Authentication;
public class ApiSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IDataProtectionProvider protection, AppDbContext db)
 : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
 public const string SchemeName="ApiSession";
 public static TicketDataFormat Format(IDataProtectionProvider provider)=>new(provider.CreateProtector("ReadManager.Api.Session.v1"));
 protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
 {
  var header=Request.Headers.Authorization.ToString();
  if(!header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))return AuthenticateResult.NoResult();
  var ticket=Format(protection).Unprotect(header[7..].Trim());
  if(ticket is null||ticket.Properties.ExpiresUtc is not { } expiry||expiry<=DateTimeOffset.UtcNow)return AuthenticateResult.Fail("Invalid session.");
  if(!int.TryParse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier),out var id))return AuthenticateResult.Fail("Invalid user.");
  var user=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.UserId==id);
  if(user is null||user.AccountStatus!="Active"||ticket.Principal.FindFirstValue("security_version")!=user.SecurityVersion.ToString()||ticket.Principal.FindFirstValue(ClaimTypes.Role)!=user.Role)return AuthenticateResult.Fail("Session revoked.");
  return AuthenticateResult.Success(ticket);
 }
}

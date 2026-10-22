using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
namespace ReadManager.Web.Services;
public class ApiCookieEvents(AuthApiClient api, DemoMemberStore members):CookieAuthenticationEvents
{
 public override async Task ValidatePrincipal(CookieValidatePrincipalContext context){
  var token=context.Properties.GetTokenValue("access_token");bool valid=false;
  if(!string.IsNullOrEmpty(token))try{using var response=await api.MeAsync(token);valid=response.IsSuccessStatusCode;}catch(HttpRequestException){}catch(TaskCanceledException){}
  // PB36 — tài khoản bị tạm ngừng thì đăng xuất ngay, không cần chờ hết phiên
  var uid=context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
  if(valid&&uid!=null&&members.StateOf(uid).State==MemberState.Suspended)valid=false;
  if(!valid){context.RejectPrincipal();await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);}
 }
}

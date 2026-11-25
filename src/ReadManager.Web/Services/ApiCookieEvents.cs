using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
namespace ReadManager.Web.Services;
public class ApiCookieEvents(AuthApiClient api):CookieAuthenticationEvents
{
 public override async Task ValidatePrincipal(CookieValidatePrincipalContext context){
  var token=context.Properties.GetTokenValue("access_token");bool valid=false;
  if(!string.IsNullOrEmpty(token))try{using var response=await api.MeAsync(token);valid=response.IsSuccessStatusCode;}catch(HttpRequestException){}catch(TaskCanceledException){}
  // PB36 — tài khoản bị tạm ngừng: API trả 401 cho /auth/me (ApiSessionHandler kiểm tra DB) → đăng xuất ngay
  if(!valid){context.RejectPrincipal();await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);}
 }
}

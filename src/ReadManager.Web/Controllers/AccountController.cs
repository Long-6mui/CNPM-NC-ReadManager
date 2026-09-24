using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;
namespace ReadManager.Web.Controllers;
public class AccountController(AuthApiClient api):Controller
{
 [HttpGet] public IActionResult Login(string? returnUrl=null)=>View(new LoginVm {ReturnUrl=returnUrl});
 [HttpGet] public IActionResult Register()=>View(new RegisterVm());
 [HttpGet] public IActionResult AccessDenied()=>View();
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Login(LoginVm vm){
  if(ModelState.IsValid)try{
   using var response=await api.LoginAsync(vm.Email.Trim(),vm.Password);
   if(response.IsSuccessStatusCode){
    var result=await response.Content.ReadFromJsonAsync<LoginResult>();
    if(result is null)throw new HttpRequestException();
    var claims=new[]{new Claim(ClaimTypes.NameIdentifier,result.User.UserId.ToString()),new Claim(ClaimTypes.Name,result.User.DisplayName),new Claim(ClaimTypes.Email,result.User.Email),new Claim(ClaimTypes.Role,result.User.Role)};
    var properties=new AuthenticationProperties {IsPersistent=vm.RememberMe,ExpiresUtc=result.ExpiresAt};properties.StoreTokens([new AuthenticationToken{Name="access_token",Value=result.AccessToken}]);
    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)),properties);
    if(Url.IsLocalUrl(vm.ReturnUrl))return LocalRedirect(vm.ReturnUrl!);
    return result.User.Role=="Admin"?RedirectToAction("Index","Stories",new {area="Admin"}):RedirectToAction("Index","Home");
   }
   ModelState.AddModelError("",response.StatusCode==System.Net.HttpStatusCode.TooManyRequests?"Bạn thử đăng nhập quá nhiều lần. Vui lòng chờ một phút.":response.StatusCode==System.Net.HttpStatusCode.Unauthorized?"Email hoặc mật khẩu không đúng, hoặc tài khoản không hoạt động.":"API chưa xử lý được đăng nhập. Vui lòng thử lại.");
  }catch(HttpRequestException){ModelState.AddModelError("","Không kết nối được API. Hãy chạy ReadManager.Api và kiểm tra địa chỉ API.");}catch(TaskCanceledException){ModelState.AddModelError("","API phản hồi quá chậm. Vui lòng thử lại.");}
  vm.Password="";ModelState.Remove(nameof(vm.Password));return View(vm);
 }
 [Authorize,HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Logout(){
  var token=await HttpContext.GetTokenAsync("access_token");
  if(token is not null)try{using var response=await api.LogoutAsync(token);}catch(HttpRequestException){}catch(TaskCanceledException){}
  await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);return RedirectToAction("Index","Home");
 }
 [HttpPost,ValidateAntiForgeryToken] public IActionResult Register(RegisterVm vm){ModelState.AddModelError("","Đăng ký chưa được kết nối API. Thông tin chưa được lưu.");vm.Password="";vm.ConfirmPassword="";ModelState.Remove(nameof(vm.Password));ModelState.Remove(nameof(vm.ConfirmPassword));return View(vm);}
}

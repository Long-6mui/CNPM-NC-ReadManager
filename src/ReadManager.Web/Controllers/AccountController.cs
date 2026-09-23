using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.ViewModels;
namespace ReadManager.Web.Controllers;
public class AccountController : Controller
{
 [HttpGet] public IActionResult Login()=>View(new LoginVm());
 [HttpGet] public IActionResult Register()=>View(new RegisterVm());
 [HttpGet] public IActionResult AccessDenied()=>View();
 [HttpPost,ValidateAntiForgeryToken] public IActionResult Login(LoginVm vm) {
  ModelState.AddModelError("","Chưa kết nối API tài khoản. Không có phiên đăng nhập nào được tạo.");
  vm.Password=""; ModelState.Remove(nameof(vm.Password)); return View(vm);
 }
 [HttpPost,ValidateAntiForgeryToken] public IActionResult Register(RegisterVm vm) {
  ModelState.AddModelError("","Chưa kết nối API tài khoản. Thông tin chưa được lưu.");
  vm.Password="";vm.ConfirmPassword="";ModelState.Remove(nameof(vm.Password));ModelState.Remove(nameof(vm.ConfirmPassword));return View(vm);
 }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace ReadManager.Web.Areas.Admin.Controllers;
public abstract class PreviewController(IWebHostEnvironment environment) : Controller
{
 public override void OnActionExecuting(ActionExecutingContext context) {
  if(!environment.IsDevelopment()) { context.Result=new NotFoundResult(); return; }
  if(User.Identity?.IsAuthenticated != true) { context.Result=new RedirectToActionResult("Login","Account",new { area="" }); return; }
  if(!User.IsInRole("Admin")) context.Result=new StatusCodeResult(403);
 }
 protected void Pending()=>ModelState.AddModelError("","Chưa kết nối API quản trị. Dữ liệu chưa được lưu.");
}

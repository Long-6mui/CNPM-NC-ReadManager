using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ReadManager.Web.Areas.Admin.Controllers;
[Authorize(Roles="Admin")]
public abstract class PreviewController(IWebHostEnvironment environment):Controller
{
 protected IWebHostEnvironment Environment {get;}=environment;
 protected void Pending()=>ModelState.AddModelError("","Chưa kết nối API quản trị. Dữ liệu chưa được lưu.");
}

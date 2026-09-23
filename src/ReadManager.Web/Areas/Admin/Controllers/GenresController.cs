using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
namespace ReadManager.Web.Areas.Admin.Controllers;
[Area("Admin")]
public class GenresController(IWebHostEnvironment environment):PreviewController(environment)
{
 public IActionResult Index(){ViewBag.Counts=DemoHomeData.GetHome(null,null).Updated.GroupBy(s=>s.GenreId).ToDictionary(g=>g.Key,g=>g.Count());return View(DemoCatalog.Genres());}
 [HttpPost,ValidateAntiForgeryToken] public IActionResult Create(string? name){TempData["Toast"]="Chưa kết nối API. Thể loại chưa được lưu.";return RedirectToAction(nameof(Index));}
 [HttpPost,ValidateAntiForgeryToken] public IActionResult Delete(int id){TempData["Toast"]="Chưa kết nối API. Thể loại chưa bị xóa.";return RedirectToAction(nameof(Index));}
}

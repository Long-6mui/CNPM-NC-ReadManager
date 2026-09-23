using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;
namespace ReadManager.Web.Areas.Admin.Controllers;
[Area("Admin")]
public class StoriesController(IWebHostEnvironment environment) : PreviewController(environment)
{
 public IActionResult Index() {var rows=DemoCatalog.Stories();ViewBag.ChapterCounts=rows.ToDictionary(s=>s.Id,s=>s.Chapters.Count);return View(rows);}
 public IActionResult Create()=>View("Edit",new StoryFormVm {AllGenres=DemoCatalog.Genres()});
 public IActionResult Edit(int id) {var s=DemoCatalog.Find(id);if(s is null)return NotFound();return View(new StoryFormVm {Id=id,Title=s.Title,Author=s.Author,Description=s.Description,Status=s.Status,Access=s.Access,AllGenres=DemoCatalog.Genres(),SelectedGenreIds=[(id-1)%3+1],Chapters=s.Chapters,Visibility="Public"});}
 [HttpPost,ValidateAntiForgeryToken] public IActionResult Save(StoryFormVm vm) {Pending();vm.AllGenres=DemoCatalog.Genres();vm.Chapters=DemoCatalog.Find(vm.Id)?.Chapters??[];return View("Edit",vm);}
 public IActionResult ChapterEdit(int storyId,int? no) {var s=DemoCatalog.Find(storyId);if(s is null)return NotFound();var c=s.Chapters.FirstOrDefault(c=>c.No==no);if(no.HasValue&&c is null)return NotFound();return View(new ChapterFormVm {Id=c?.Id??0,StoryId=storyId,StoryTitle=s.Title,No=c?.No??s.Chapters.Count+1,Title=c?.Title??"",Content=c?.Content??"",IsFree=c?.IsFree??true,Status=c?.Status??ChapterStatus.Draft});}
 [HttpPost,ValidateAntiForgeryToken] public IActionResult ChapterSave(ChapterFormVm vm) {Pending();return View("ChapterEdit",vm);}
 public IActionResult BulkImport(int storyId) {var s=DemoCatalog.Find(storyId);return s is null?NotFound():View(new BulkImportVm {StoryId=storyId,StoryTitle=s.Title});}
 [HttpPost,ValidateAntiForgeryToken] public IActionResult BulkImportSave(BulkImportVm vm) {
  if(string.IsNullOrWhiteSpace(vm.RawText)||vm.RawText.Length>200000)ModelState.AddModelError("","Nhập nội dung từ 1 đến 200.000 ký tự.");
  else {var result=ChapterBulkParser.Parse(vm.RawText);ModelState.AddModelError("",$"Nhận diện {result.Count} chương. Đây là kiểm tra nội dung; chưa lưu DB.");}
  return View("BulkImport",vm);
 }
 [HttpPost,ValidateAntiForgeryToken] public IActionResult Delete(int id){TempData["Toast"]="Chưa kết nối API. Không có truyện nào bị xóa.";return RedirectToAction(nameof(Index));}
 [HttpPost,ValidateAntiForgeryToken] public IActionResult ChapterDelete(int storyId,int no){TempData["Toast"]="Chưa kết nối API. Không có chương nào bị xóa.";return RedirectToAction(nameof(Edit),new{id=storyId});}
}

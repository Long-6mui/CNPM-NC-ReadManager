using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;
namespace ReadManager.Web.Controllers;
public class StoriesController : Controller
{
 public IActionResult Browse(string? q,int? genre,AccessPolicy? access,StoryStatus? status,string sort="updated") {
  var home=DemoHomeData.GetHome(q?.Trim(),genre);
  var rows=home.Updated.Where(x=>(!access.HasValue||x.Access==access)&&(!status.HasValue||x.Status==status));
  rows=sort switch {"newest"=>rows.OrderByDescending(x=>x.CreatedAtSort),"title"=>rows.OrderBy(x=>x.Title),_=>rows.OrderByDescending(x=>x.UpdatedAtSort)};
  return View(new BrowseVm {Stories=rows.ToList(),Genres=home.Genres,Q=q,GenreId=genre,Access=access,Status=status,Sort=sort});
 }
 public IActionResult Details(int id) {
  var story=DemoCatalog.Find(id); if(story is null)return NotFound();
  return View(new StoryDetailVm {Story=story,GenreNames=[DemoCatalog.Genres().First(x=>x.Id==(id-1)%3+1).Name],FirstVisibleChapterNo=1,
   Chapters=story.Chapters.Select(c=>new ChapterRowVm {No=c.No,Title=c.Title,Locked=!c.IsFree,CreatedAt=c.CreatedAt}).ToList()});
 }
 public IActionResult Read(int storyId,int no) {
  var story=DemoCatalog.Find(storyId); var chapter=story?.Chapters.FirstOrDefault(c=>c.No==no);
  if(story is null||chapter is null)return NotFound();
  var locked=!chapter.IsFree; if(locked)chapter.Content="";
  return View(new ReadVm {Story=story,Chapter=chapter,Locked=locked,PrevNo=no>1?no-1:null,NextNo=no<story.Chapters.Count?no+1:null});
 }
}

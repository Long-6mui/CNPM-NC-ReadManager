using ReadManager.Web.ViewModels;
namespace ReadManager.Web.Services;
// UI preview only: never persists changes or grants real reading rights.
public static class DemoCatalog
{
 public static List<GenreVm> Genres() => DemoHomeData.GetHome(null,null).Genres;
 public static Story? Find(int id)
 {
  var card=DemoHomeData.GetHome(null,null).Updated.FirstOrDefault(x=>x.Id==id);
  if(card is null) return null;
  var s=new Story {Id=id,Title=card.Title,Author=card.Author,Access=card.Access,Status=card.Status,
   Description="Câu chuyện minh họa dùng để kiểm tra giao diện. Nội dung thật sẽ được lấy từ API của nhóm.",
   Price=card.Access==AccessPolicy.Free?0:50000, UpdatedAt=card.UpdatedAtSort,FreeChapterCount=card.Access==AccessPolicy.Free?card.PublishedChapterCount:1};
  s.Chapters=Enumerable.Range(1,card.PublishedChapterCount).Select(n=>new Chapter {
   Id=id*100+n,StoryId=id,No=n,Title=n==1?"Khởi đầu hành trình":"Những trang tiếp theo "+n,
   IsFree=card.Access==AccessPolicy.Free||n==1,Status=ChapterStatus.Reviewed,CreatedAt=new DateTime(2026,9,1),
   Content="Buổi sáng, ánh nắng len qua khung cửa. Một hành trình mới bắt đầu từ trang sách còn dang dở.\n\nĐây là nội dung mẫu của chương "+n+". Bạn có thể chuyển chương, thay đổi cỡ chữ và quay về danh sách chương để kiểm tra giao diện."
  }).ToList(); return s;
 }
 public static List<Story> Stories()=>DemoHomeData.GetHome(null,null).Updated.Select(x=>Find(x.Id)!).ToList();
}

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace ReadManager.Api.Data;
public static class ZipStoryImporter
{
 public static async Task<string> ImportAsync(AppDbContext db)
 {
  if(!await db.Database.CanConnectAsync()) throw new InvalidOperationException("Cannot connect to configured database.");
  if((await db.Database.GetPendingMigrationsAsync()).Any())throw new InvalidOperationException("Apply existing migrations before importing.");
  await using var transaction=await db.Database.BeginTransactionAsync();
  var genreNames=new[] {"Ngôn tình","Tiên hiệp","Trinh thám","Đô thị","Huyền huyễn","Lịch sử","Khoa huyễn","Đoản văn"};
  var genres=new List<Entities.Genre>();
  foreach(var name in genreNames) {
   var genre=await db.Genres.FirstOrDefaultAsync(x=>x.Name==name);
   if(genre is null){genre=new Entities.Genre{Name=name,Slug=Slug(name)};db.Genres.Add(genre);}
   genres.Add(genre);
  }
  Entities.Genre G(string name)=>genres.First(x=>x.Name==name);
        var story1 = new Story
        {
            Title = "Gió Mùa Thu Trên Mái Ngói",
            Author = "Lam Khê",
            Description = "Một kiến trúc sư trẻ trở về phố cổ trùng tu căn nhà của bà ngoại và tình cờ tìm lại người bạn thời thơ ấu.",
            Status = "Ongoing",
            Access = "Mixed",
            FreeChapterCount = 2,
            Price = 29000,
        };
        story1.StoryGenres.Add(new StoryGenre { Genre = G("Ngôn tình") });
        story1.StoryGenres.Add(new StoryGenre { Genre = G("Đô thị") });
        story1.Chapters.Add(new Chapter { No = 1, Title = "Mái ngói cũ", Content = "Hạ về phố cổ vào một chiều tháng Chín, khi nắng đã ngả sang màu mật ong loãng.\n\nCô đặt vali xuống bậc thềm, ngước nhìn khoảng trời bị mái nhà cắt thành một dải hẹp.", IsFree = true, Status = "Published" });
        story1.Chapters.Add(new Chapter { No = 2, Title = "Người quen cũ", Content = "Người đàn ông đứng trước cửa khoác áo bảo hộ lấm bụi vôi, tay còn cầm chiếc bay trát vữa.\n\n\"Nam?\" cô hỏi, giọng còn chưa hết ngỡ ngàng.", IsFree = true, Status = "Published" });
        story1.Chapters.Add(new Chapter { No = 3, Title = "Buổi chiều mùi vôi vữa", Content = "Những ngày sau đó, Hạ dành phần lớn thời gian ngồi trên bậc thềm, phác thảo lại bản vẽ trùng tu.\n\n\"Em vẫn giữ thói quen vẽ trước khi ngủ à?\" Nam hỏi.", IsFree = false, Status = "Published" });

        var story2 = new Story
        {
            Title = "Bến Cảm Thành Long",
            Author = "Đường Vũ",
            Description = "Thiếu niên mồ côi Kỳ Phong nhặt được một mảnh ngọc bội cổ xưa, mở ra hành trình tu luyện giữa chín châu.",
            Status = "Ongoing",
            Access = "Paid",
            FreeChapterCount = 1,
            Price = 49000,
        };
        story2.StoryGenres.Add(new StoryGenre { Genre = G("Tiên hiệp") });
        story2.StoryGenres.Add(new StoryGenre { Genre = G("Huyền huyễn") });
        story2.Chapters.Add(new Chapter { No = 1, Title = "Mảnh ngọc bến sông", Content = "Kỳ Phong tìm thấy mảnh ngọc bội khi nước sông rút cạn vào cuối thu.\n\nĐêm đó, lần đầu tiên cậu mơ thấy một con rồng xanh bay ngang bầu trời không trăng.", IsFree = true, Status = "Published" });
        story2.Chapters.Add(new Chapter { No = 2, Title = "Thử luyện đầu tiên", Content = "Đạo sĩ Vân Thanh Tử là người đầu tiên nhận ra khí tức lạ toát ra từ Kỳ Phong.\n\n\"Tu luyện không phải để mạnh hơn người khác, mà để không thẹn với lòng mình.\"", IsFree = false, Status = "Published" });

        var story3 = new Story
        {
            Title = "Hồ Sơ Không Tên",
            Author = "Minh Vỹ",
            Description = "Nữ thám tử tư Diệu An nhận một vụ án tưởng chừng đơn giản, dẫn cô vào một đường dây che giấu suốt hai mươi năm.",
            Status = "Completed",
            Access = "Free",
            FreeChapterCount = 999,
            Price = 0,
        };
        story3.StoryGenres.Add(new StoryGenre { Genre = G("Trinh thám") });
        story3.Chapters.Add(new Chapter { No = 1, Title = "Bức thư thất lạc", Content = "Người phụ nữ ngồi đối diện Diệu An run run đặt lên bàn một chiếc phong bì ố vàng.\n\n\"Tôi cần tìm bức thư gốc của bức thư này,\" bà nói.", IsFree = true, Status = "Published" });
        story3.Chapters.Add(new Chapter { No = 2, Title = "Thị trấn không còn tên", Content = "Hồ sơ lưu trữ của tòa thị chính chỉ còn lại vài trang cháy xém.", IsFree = true, Status = "Published" });


  var owner=await db.Users.FirstOrDefaultAsync(u=>u.Username=="zip-import-owner");
  if(owner is null){owner=new Entities.User{Username="zip-import-owner",Email="zip-import-owner@example.invalid",DisplayName="Nguồn truyện ZIP",Role="Member",AccountStatus="Disabled"};owner.PasswordHash=new PasswordHasher<Entities.User>().HashPassword(owner,Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));db.Users.Add(owner);}
  await db.SaveChangesAsync();
  int addedStories=0,addedChapters=0,addedLinks=0;
  foreach(var input in new[]{story1,story2,story3}) {
   var slug=Slug(input.Title);
   var target=await db.Stories.FirstOrDefaultAsync(s=>s.Slug==slug);
   if(target is null){target=new Entities.Story{Title=input.Title,Slug=slug,AuthorName=input.Author,Synopsis=input.Description,PublicationStatus=input.Status,Visibility="Public",AccessPolicy=input.Access,CurrentPrice=input.Price>0?input.Price:null,CreatedBy=owner.UserId,FirstPublishedAt=DateTime.UtcNow};db.Stories.Add(target);await db.SaveChangesAsync();addedStories++;}
   foreach(var link in input.StoryGenres){if(!await db.StoryGenres.AnyAsync(x=>x.StoryId==target.StoryId&&x.GenreId==link.Genre.GenreId)){db.StoryGenres.Add(new Entities.StoryGenre{StoryId=target.StoryId,GenreId=link.Genre.GenreId});addedLinks++;}}
   foreach(var c in input.Chapters){if(!await db.Chapters.AnyAsync(x=>x.StoryId==target.StoryId&&x.ChapterNumber==c.No)){db.Chapters.Add(new Entities.Chapter{StoryId=target.StoryId,ChapterNumber=c.No,Title=c.Title,Content=c.Content,AccessLevel=c.IsFree?"Free":"Paid",PublicationStatus="Published",PublishedAt=DateTime.UtcNow});addedChapters++;}}
   await db.SaveChangesAsync();
  }
  await transaction.CommitAsync();
  return $"Added stories={addedStories}, chapters={addedChapters}, links={addedLinks}. Database totals: genres={await db.Genres.CountAsync()}, stories={await db.Stories.CountAsync()}, chapters={await db.Chapters.CountAsync()}.";
 }
 private static string Slug(string value){var decomposed=value.ToLowerInvariant().Replace('đ','d').Normalize(NormalizationForm.FormD);var plain=new string(decomposed.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray());return Regex.Replace(plain,"[^a-z0-9]+","-").Trim('-');}
 private sealed class Story{public string Title{get;set;}="";public string Author{get;set;}="";public string Description{get;set;}="";public string Status{get;set;}="";public string Access{get;set;}="";public int FreeChapterCount{get;set;}public decimal Price{get;set;}public List<StoryGenre> StoryGenres{get;}=[];public List<Chapter> Chapters{get;}=[];}
 private sealed class StoryGenre{public Entities.Genre Genre{get;set;}=null!;}
 private sealed class Chapter{public int No{get;set;}public string Title{get;set;}="";public string Content{get;set;}="";public bool IsFree{get;set;}public string Status{get;set;}="";}
}

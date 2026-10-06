using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Authentication;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;
using ReadManager.Api.Services;
using System.Threading.RateLimiting;

<<<<<<< Updated upstream
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<IStoryService, StoryService>();
builder.Services.AddScoped<IGenreService, GenreService>();
builder.Services.AddScoped<IChapterService, ChapterService>(); // Backend 4 — chương/đọc

builder.Services.AddDataProtection()
    .SetApplicationName("ReadManager.Api");

builder.Services.AddAuthentication(ApiSessionHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiSessionHandler>(
        ApiSessionHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
=======
try
>>>>>>> Stashed changes
{
    var builder = WebApplication.CreateBuilder(args);

    // 1. Lấy chuỗi kết nối MySQL
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:DefaultConnection.");

    // Cấu hình Entity Framework dùng MySQL
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString),
            mySqlOptions =>
            {
                mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            })
    );

    // 2. Cấu hình CORS tương thích đầy đủ với Web và Cookie/Session
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    builder.Services.AddScoped<IStoryService, StoryService>();
    builder.Services.AddScoped<IGenreService, GenreService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    builder.Services.AddDataProtection()
        .SetApplicationName("ReadManager.Api");

    builder.Services.AddAuthentication(ApiSessionHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, ApiSessionHandler>(
            ApiSessionHandler.SchemeName,
            _ => { });

    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = 429;
        options.AddPolicy("login", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // 3. Tự động khởi tạo CSDL MySQL và nạp dữ liệu mẫu
    if (app.Environment.IsDevelopment())
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Tự động tạo schema và bảng trong MySQL nếu chưa tồn tại
            await db.Database.EnsureCreatedAsync();

            if (!await db.Genres.AnyAsync())
            {
                await SeedData(db);
            }
        }

        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 4. Cấu hình Middleware Pipeline
    app.UseHttpsRedirection();
    app.UseCors("AllowAll");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();

    // 5. Hàm nạp dữ liệu mẫu
    async Task SeedData(AppDbContext db)
    {
        var genres = new[]
        {
            new Genre { Name = "Ngôn Tình", Slug = "ngon-tinh" },
            new Genre { Name = "Tiên Hiệp", Slug = "tien-hiep" },
            new Genre { Name = "Khoa Vũ Trụ", Slug = "khoa-vu-tru" },
            new Genre { Name = "Hành Động", Slug = "hanh-dong" },
            new Genre { Name = "Kỳ Ảo", Slug = "ky-ao" },
        };
        db.Genres.AddRange(genres);
        await db.SaveChangesAsync();

        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@example.com",
            DisplayName = "Admin",
            Role = "Admin",
            AccountStatus = "Active",
            SecurityVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        adminUser.PasswordHash = new PasswordHasher<User>().HashPassword(adminUser, "Admin@123");
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        var stories = new[]
        {
            new Story
            {
                Title = "Tây Du Ký",
                Slug = "tay-du-ky",
                AuthorName = "Ngô Thừa Ân",
                Synopsis = "Chuyện lâu đời về hành trình tây du của Đường Tăng và ba người đệ tử.",
                PublicationStatus = "Completed",
                Visibility = "Public",
                AccessPolicy = "Free",
                CreatedBy = adminUser.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                FirstPublishedAt = DateTime.UtcNow
            },
            new Story
            {
                Title = "Mộng Của Em",
                Slug = "mong-cua-em",
                AuthorName = "Tác giả Ẩn danh",
                Synopsis = "Câu chuyện tình yêu đầy cảm xúc giữa hai trái tim yêu nhau.",
                PublicationStatus = "Ongoing",
                Visibility = "Public",
                AccessPolicy = "Free",
                CreatedBy = adminUser.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                FirstPublishedAt = DateTime.UtcNow
            }
        };
        db.Stories.AddRange(stories);
        await db.SaveChangesAsync();

        var chapters = new[]
        {
            new Chapter
            {
                StoryId = stories[0].StoryId,
                ChapterNumber = 1,
                Title = "Chương 1: Bắt đầu",
                Content = "Ngày xưa, ở đất Trung Hoa, có một nơi tên là Đông Thăng Bộ...",
                AccessLevel = "Free",
                PublicationStatus = "Published",
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Chapter
            {
                StoryId = stories[1].StoryId,
                ChapterNumber = 1,
                Title = "Chương 1: Gặp Gỡ",
                Content = "Nó là một chiều mưa nặng hạt, cô ấy bước vào quán cà phê...",
                AccessLevel = "Free",
                PublicationStatus = "Published",
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        db.Chapters.AddRange(chapters);
        await db.SaveChangesAsync();
    }

    app.Run();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("=================================================");
    Console.WriteLine($"[CRITICAL STARTUP ERROR]: {ex.Message}");
    Console.WriteLine(ex.ToString());
    Console.WriteLine("=================================================");
    Console.ResetColor();
    Console.WriteLine("Nhấn phím bất kỳ để thoát console...");
    Console.ReadKey();
}
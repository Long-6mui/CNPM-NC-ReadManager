using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<StoryGenre> StoryGenres => Set<StoryGenre>();

    public DbSet<ReadingPlan> ReadingPlans => Set<ReadingPlan>();

    public DbSet<ReadingPlanVersion> ReadingPlanVersions
        => Set<ReadingPlanVersion>();

    public DbSet<PlanVersionStory> PlanVersionStories
        => Set<PlanVersionStory>();

    // ----- BE4: Tương tác (PB31–33) -----
    public DbSet<StoryFollow> StoryFollows => Set<StoryFollow>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Report> Reports => Set<Report>();

    // ----- BE3: Đơn hàng (PB19, PB21, PB26) -----
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4");
        modelBuilder.UseCollation("utf8mb4_unicode_ci");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).ValueGeneratedOnAdd();
            entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AvatarUrl).HasMaxLength(2048).IsRequired(false);
            entity.Property(x => x.Role).HasMaxLength(20).HasDefaultValue("Member").IsRequired();
            entity.Property(x => x.AccountStatus).HasMaxLength(20).HasDefaultValue("Active").IsRequired();
            entity.Property(x => x.SecurityVersion).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.UpdatedAt).HasColumnType("datetime(6)");
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.ToTable("Stories", table =>
                table.HasCheckConstraint("CK_Stories_CurrentPrice", "`CurrentPrice` IS NULL OR `CurrentPrice` > 0"));
            entity.HasKey(x => x.StoryId);
            entity.Property(x => x.StoryId).ValueGeneratedOnAdd();
            entity.Property(x => x.Title).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.AuthorName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Synopsis).HasColumnType("text").IsRequired();
            entity.Property(x => x.CoverUrl).HasMaxLength(2048).IsRequired(false);
            entity.Property(x => x.PublicationStatus).HasMaxLength(20).HasDefaultValue("Ongoing").IsRequired();
            entity.Property(x => x.Visibility).HasMaxLength(20).HasDefaultValue("Draft").IsRequired();
            entity.Property(x => x.AccessPolicy).HasMaxLength(20).HasDefaultValue("Free").IsRequired();
            entity.Property(x => x.CurrentPrice).HasPrecision(12, 2).IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.UpdatedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.FirstPublishedAt).HasColumnType("datetime(6)");
            entity.HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.ToTable("Chapters", table =>
                table.HasCheckConstraint("CK_Chapters_ChapterNumber", "`ChapterNumber` > 0"));
            entity.HasKey(x => x.ChapterId);
            entity.Property(x => x.ChapterId).ValueGeneratedOnAdd();
            // Alternate key để ReadingProgress tham chiếu khóa ghép ở sprint sau.
            entity.HasAlternateKey(x => new { x.StoryId, x.ChapterNumber });
            entity.Property(x => x.Title).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Content).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.AccessLevel).HasMaxLength(10).HasDefaultValue("Free").IsRequired();
            entity.Property(x => x.PublicationStatus).HasMaxLength(20).HasDefaultValue("Draft").IsRequired();
            entity.Property(x => x.ScheduledAt).HasColumnType("datetime(6)");
            entity.Property(x => x.PublishedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.UpdatedAt).HasColumnType("datetime(6)");
            entity.HasOne(x => x.Story)
                .WithMany()
                .HasForeignKey(x => x.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("Genres");
            entity.HasKey(x => x.GenreId);
            entity.Property(x => x.GenreId).ValueGeneratedOnAdd();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<StoryGenre>(entity =>
        {
            entity.ToTable("StoryGenres");
            entity.HasKey(x => new { x.StoryId, x.GenreId });
            entity.HasOne(x => x.Story)
                .WithMany()
                .HasForeignKey(x => x.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Genre)
                .WithMany()
                .HasForeignKey(x => x.GenreId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReadingPlan>(entity =>
        {
            entity.ToTable("ReadingPlans");

            entity.HasKey(x => x.PlanId);

            entity.Property(x => x.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnType("datetime(6)");
        });

        modelBuilder.Entity<ReadingPlanVersion>(entity =>
        {
            entity.ToTable("ReadingPlanVersions", table =>
            {
                table.HasCheckConstraint(
                    "CK_ReadingPlanVersions_Price",
                    "`Price` > 0");

                table.HasCheckConstraint(
                    "CK_ReadingPlanVersions_DurationMonths",
                    "`DurationMonths` > 0");

                table.HasCheckConstraint(
                    "CK_ReadingPlanVersions_VersionNumber",
                    "`VersionNumber` > 0");

                table.HasCheckConstraint(
                    "CK_ReadingPlanVersions_ScopeType",
                    "`ScopeType` IN ('All', 'Selected')");
            });

            entity.HasKey(x => x.PlanVersionId);

            entity.HasIndex(x => new { x.PlanId, x.VersionNumber })
                .IsUnique();

            entity.Property(x => x.Price)
                .HasPrecision(18, 2);

            entity.Property(x => x.ScopeType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnType("datetime(6)");

            entity.Property(x => x.PublishedAt)
                .HasColumnType("datetime(6)");

            entity.HasOne(x => x.Plan)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlanVersionStory>(entity =>
        {
            entity.ToTable("PlanVersionStories");

            entity.HasKey(x => new { x.PlanVersionId, x.StoryId });

            entity.HasOne(x => x.PlanVersion)
                .WithMany(x => x.PlanVersionStories)
                .HasForeignKey(x => x.PlanVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Story)
                .WithMany()
                .HasForeignKey(x => x.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== BE4: TƯƠNG TÁC (PB31–33) =====

        modelBuilder.Entity<StoryFollow>(entity =>
        {
            entity.ToTable("StoryFollows");
            entity.HasKey(x => new { x.UserId, x.StoryId });   // 1 người theo dõi 1 truyện 1 lần
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Story).WithMany().HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(x => x.NotificationId);
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.Property(x => x.IsRead).HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });   // lấy thông báo của 1 người cho nhanh
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Story).WithMany().HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.Restrict);
            // Xóa chương → xóa luôn thông báo của chương đó (nếu không, admin sẽ không xóa được chương)
            entity.HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("Comments");
            entity.HasKey(x => x.CommentId);
            entity.Property(x => x.Content).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Visible").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.HasIndex(x => new { x.StoryId, x.ChapterId, x.CreatedAt });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Story).WithMany().HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.Restrict);
            // Xóa chương → bình luận vẫn giữ, chỉ bỏ liên kết chương (thành bình luận chung của truyện)
            entity.HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.ToTable("Reports", table =>
                table.HasCheckConstraint("CK_Reports_TargetType", "`TargetType` IN ('Story', 'Chapter', 'Comment')"));
            entity.HasKey(x => x.ReportId);
            entity.Property(x => x.TargetType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500).IsRequired(false);
            entity.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.HandledAt).HasColumnType("datetime(6)");
            entity.HasIndex(x => new { x.Status, x.CreatedAt });   // admin lọc báo cáo chờ xử lý
            entity.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        });

        // ===== BE3: ĐƠN HÀNG (PB19, PB21, PB26) =====
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", table =>
            {
                table.HasCheckConstraint("CK_Orders_OrderType", "`OrderType` IN ('Plan', 'Story')");
                table.HasCheckConstraint("CK_Orders_Status", "`Status` IN ('Pending', 'Paid', 'Failed', 'Cancelled', 'Expired')");
                table.HasCheckConstraint("CK_Orders_Amount", "`Amount` > 0");
                // Mua gói phải có PlanVersionId, mua truyện phải có StoryId
                table.HasCheckConstraint("CK_Orders_Item",
                    "(`OrderType` = 'Plan' AND `PlanVersionId` IS NOT NULL) OR (`OrderType` = 'Story' AND `StoryId` IS NOT NULL)");
            });
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.OrderType).HasMaxLength(10).IsRequired();
            entity.Property(x => x.ItemName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.UpdatedAt).HasColumnType("datetime(6)");
            entity.Property(x => x.PaidAt).HasColumnType("datetime(6)");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });   // lịch sử thanh toán của 1 người
            entity.HasIndex(x => new { x.Status, x.CreatedAt });   // Admin lọc theo trạng thái / quét đơn hết hạn
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PlanVersion).WithMany().HasForeignKey(x => x.PlanVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Story).WithMany().HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

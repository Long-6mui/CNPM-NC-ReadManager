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
    }
}

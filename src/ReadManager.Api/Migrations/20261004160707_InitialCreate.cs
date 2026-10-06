using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    GenreId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.GenreId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Member"),
                    AccountStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Active"),
                    SecurityVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Stories",
                columns: table => new
                {
                    StoryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Synopsis = table.Column<string>(type: "text", nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    PublicationStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Ongoing"),
                    Visibility = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    AccessPolicy = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Free"),
                    CurrentPrice = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    CreatedBy = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FirstPublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stories", x => x.StoryId);
                    table.ForeignKey(
                        name: "FK_Stories_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Chapters",
                columns: table => new
                {
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "longtext", nullable: false),
                    AccessLevel = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "Free"),
                    PublicationStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    ScheduledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chapters", x => x.ChapterId);
                    table.UniqueConstraint("AK_Chapters_StoryId_ChapterNumber", x => new { x.StoryId, x.ChapterNumber });
                    table.ForeignKey(
                        name: "FK_Chapters_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "StoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoryGenres",
                columns: table => new
                {
                    StoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    GenreId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryGenres", x => new { x.StoryId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_StoryGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "GenreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryGenres_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "StoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Slug",
                table: "Genres",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stories_CreatedBy",
                table: "Stories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Slug",
                table: "Stories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoryGenres_GenreId",
                table: "StoryGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chapters");

            migrationBuilder.DropTable(
                name: "StoryGenres");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "Stories");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}

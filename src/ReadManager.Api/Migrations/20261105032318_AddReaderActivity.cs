using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GenrePromptCompleted",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChapterRewards",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ChapterId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterRewards", x => new { x.UserId, x.ChapterId });
                    table.ForeignKey(
                        name: "FK_ChapterRewards_Chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapters",
                        principalColumn: "ChapterId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChapterRewards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "ReaderFavorites",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderFavorites", x => new { x.UserId, x.StoryId });
                    table.ForeignKey(
                        name: "FK_ReaderFavorites_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "StoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReaderFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "ReaderGenres",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderGenres", x => new { x.UserId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_ReaderGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "GenreId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReaderGenres_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterRewards_ChapterId",
                table: "ChapterRewards",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReaderFavorites_StoryId",
                table: "ReaderFavorites",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReaderGenres_GenreId",
                table: "ReaderGenres",
                column: "GenreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChapterRewards");

            migrationBuilder.DropTable(
                name: "ReaderFavorites");

            migrationBuilder.DropTable(
                name: "ReaderGenres");

            migrationBuilder.DropColumn(
                name: "GenrePromptCompleted",
                table: "Users");
        }
    }
}

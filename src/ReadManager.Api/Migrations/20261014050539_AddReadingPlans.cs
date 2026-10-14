using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReadingPlans",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAvailable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingPlans", x => x.PlanId);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "ReadingPlanVersions",
                columns: table => new
                {
                    PlanVersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    ScopeType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingPlanVersions", x => x.PlanVersionId);
                    table.CheckConstraint("CK_ReadingPlanVersions_DurationMonths", "`DurationMonths` > 0");
                    table.CheckConstraint("CK_ReadingPlanVersions_Price", "`Price` > 0");
                    table.CheckConstraint("CK_ReadingPlanVersions_ScopeType", "`ScopeType` IN ('All', 'Selected')");
                    table.CheckConstraint("CK_ReadingPlanVersions_VersionNumber", "`VersionNumber` > 0");
                    table.ForeignKey(
                        name: "FK_ReadingPlanVersions_ReadingPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "ReadingPlans",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "PlanVersionStories",
                columns: table => new
                {
                    PlanVersionId = table.Column<int>(type: "int", nullable: false),
                    StoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanVersionStories", x => new { x.PlanVersionId, x.StoryId });
                    table.ForeignKey(
                        name: "FK_PlanVersionStories_ReadingPlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "ReadingPlanVersions",
                        principalColumn: "PlanVersionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanVersionStories_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "StoryId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersionStories_StoryId",
                table: "PlanVersionStories",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingPlanVersions_PlanId_VersionNumber",
                table: "ReadingPlanVersions",
                columns: new[] { "PlanId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanVersionStories");

            migrationBuilder.DropTable(
                name: "ReadingPlanVersions");

            migrationBuilder.DropTable(
                name: "ReadingPlans");
        }
    }
}

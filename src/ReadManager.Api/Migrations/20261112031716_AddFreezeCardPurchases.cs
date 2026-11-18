using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFreezeCardPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FreezeCardPurchases",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Cost = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreezeCardPurchases", x => new { x.UserId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_FreezeCardPurchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreezeCardPurchases");
        }
    }
}

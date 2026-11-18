using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersPaidAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_PaidAt",
                table: "Orders",
                columns: new[] { "Status", "PaidAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_PaidAt",
                table: "Orders");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ReadManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVouchersAndOrderVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VoucherId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherName",
                table: "Orders",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VoucherOffers",
                columns: table => new
                {
                    OfferId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CoinCost = table.Column<int>(type: "int", nullable: false),
                    DiscountType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MaxDiscount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    ValidDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherOffers", x => x.OfferId);
                    table.CheckConstraint("CK_VoucherOffers_CoinCost", "`CoinCost` > 0");
                    table.CheckConstraint("CK_VoucherOffers_DiscountType", "`DiscountType` IN ('Percent', 'Amount')");
                    table.CheckConstraint("CK_VoucherOffers_ValidDays", "`ValidDays` > 0");
                    table.CheckConstraint("CK_VoucherOffers_Value", "`Value` > 0");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    VoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OfferId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DiscountType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MaxDiscount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    CoinCost = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Available", collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.VoucherId);
                    table.CheckConstraint("CK_Vouchers_DiscountType", "`DiscountType` IN ('Percent', 'Amount')");
                    table.CheckConstraint("CK_Vouchers_Status", "`Status` IN ('Available', 'Reserved', 'Used', 'Expired')");
                    table.CheckConstraint("CK_Vouchers_Value", "`Value` > 0");
                    table.ForeignKey(
                        name: "FK_Vouchers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Vouchers_VoucherOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "VoucherOffers",
                        principalColumn: "OfferId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.InsertData(
                table: "VoucherOffers",
                columns: new[] { "OfferId", "CoinCost", "CreatedAt", "Description", "DiscountType", "IsActive", "MaxDiscount", "MinOrderAmount", "Name", "SortOrder", "ValidDays", "Value" },
                values: new object[,]
                {
                    { 1, 30, new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Áp dụng cho đơn mua trọn bộ truyện từ 20.000₫", "Percent", true, 15000m, 20000m, "Giảm 10% đơn truyện", 1, 30, 10m },
                    { 2, 60, new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Giảm tối đa 30.000₫ cho đơn mua truyện từ 30.000₫", "Percent", true, 30000m, 30000m, "Giảm 20% đơn truyện", 2, 30, 20m },
                    { 3, 40, new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Áp dụng cho đơn mua trọn bộ truyện từ 25.000₫", "Amount", true, null, 25000m, "Giảm 10.000₫ mua truyện", 3, 30, 10000m },
                    { 4, 90, new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Áp dụng cho đơn mua trọn bộ truyện từ 45.000₫", "Amount", true, null, 45000m, "Giảm 25.000₫ truyện VIP", 4, 30, 25000m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherOffers_IsActive_SortOrder",
                table: "VoucherOffers",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_OfferId",
                table: "Vouchers",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_UserId_RequestId",
                table: "Vouchers",
                columns: new[] { "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_UserId_Status_ExpiresAt",
                table: "Vouchers",
                columns: new[] { "UserId", "Status", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "VoucherId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "VoucherOffers");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherName",
                table: "Orders");
        }
    }
}

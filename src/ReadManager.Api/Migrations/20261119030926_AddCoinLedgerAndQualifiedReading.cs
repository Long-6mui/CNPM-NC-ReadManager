using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCoinLedgerAndQualifiedReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoinTransactions",
                columns: table => new
                {
                    CoinTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<long>(type: "bigint", nullable: false),
                    SourceKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kind = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinTransactions", x => x.CoinTransactionId);
                    table.ForeignKey(
                        name: "FK_CoinTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "QualifiedReadingDays",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualifiedReadingDays", x => new { x.UserId, x.ReadDate });
                    table.ForeignKey(
                        name: "FK_QualifiedReadingDays_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserId_CoinTransactionId",
                table: "CoinTransactions",
                columns: new[] { "UserId", "CoinTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserId_SourceKey",
                table: "CoinTransactions",
                columns: new[] { "UserId", "SourceKey" },
                unique: true);
            // Preserve balances from the previous counting-based wallet.
            // Historical event times were not stored, so record one honest opening balance.
            migrationBuilder.Sql(@"
                INSERT INTO CoinTransactions (UserId, Amount, BalanceAfter, SourceKey, Kind, Description, CreatedAt)
                SELECT u.UserId,
                    10 * (SELECT COUNT(*) FROM ChapterRewards r WHERE r.UserId = u.UserId)
                    + COALESCE(s.EarnedStreakCoins, 0)
                    - COALESCE((SELECT SUM(p.Cost) FROM FreezeCardPurchases p WHERE p.UserId = u.UserId), 0),
                    10 * (SELECT COUNT(*) FROM ChapterRewards r WHERE r.UserId = u.UserId)
                    + COALESCE(s.EarnedStreakCoins, 0)
                    - COALESCE((SELECT SUM(p.Cost) FROM FreezeCardPurchases p WHERE p.UserId = u.UserId), 0),
                    'opening', 'OpeningBalance', 'Số dư trước khi bổ sung lịch sử xu', UTC_TIMESTAMP()
                FROM Users u LEFT JOIN UserStreaks s ON s.UserId = u.UserId");
            migrationBuilder.Sql(@"
                INSERT INTO QualifiedReadingDays (UserId, ReadDate)
                SELECT UserId, LastReadDate FROM UserStreaks
                WHERE CurrentStreak > 0 AND LastReadDate IS NOT NULL");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoinTransactions");

            migrationBuilder.DropTable(
                name: "QualifiedReadingDays");
        }
    }
}

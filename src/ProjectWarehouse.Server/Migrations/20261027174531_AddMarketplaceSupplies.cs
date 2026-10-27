using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceSupplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplyId",
                table: "MarketplaceOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketplaceSupplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    OrdersSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceSupplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceSupplies_MarketplaceAccounts_MarketplaceAccountId",
                        column: x => x.MarketplaceAccountId,
                        principalTable: "MarketplaceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_SupplyId",
                table: "MarketplaceOrders",
                column: "SupplyId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceSupplies_CreatedAt",
                table: "MarketplaceSupplies",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceSupplies_MarketplaceAccountId_ExternalId",
                table: "MarketplaceSupplies",
                columns: new[] { "MarketplaceAccountId", "ExternalId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MarketplaceOrders_MarketplaceSupplies_SupplyId",
                table: "MarketplaceOrders",
                column: "SupplyId",
                principalTable: "MarketplaceSupplies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MarketplaceOrders_MarketplaceSupplies_SupplyId",
                table: "MarketplaceOrders");

            migrationBuilder.DropTable(
                name: "MarketplaceSupplies");

            migrationBuilder.DropIndex(
                name: "IX_MarketplaceOrders_SupplyId",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "SupplyId",
                table: "MarketplaceOrders");
        }
    }
}

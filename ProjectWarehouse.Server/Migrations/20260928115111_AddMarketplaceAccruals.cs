using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceAccruals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccrualsCreated",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccrualsProcessed",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccrualsUpdated",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccrualsFullPassAt",
                table: "MarketplaceAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccrualsSyncedAt",
                table: "MarketplaceAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketplaceAccruals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    RawTypeId = table.Column<string>(type: "text", nullable: false),
                    UnitNumber = table.Column<string>(type: "text", nullable: true),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderMarketplaceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceAccruals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceAccruals_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketplaceAccruals_MarketplaceAccounts_MarketplaceAccountId",
                        column: x => x.MarketplaceAccountId,
                        principalTable: "MarketplaceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplaceAccruals_OrderMarketplaceItems_OrderMarketplaceI~",
                        column: x => x.OrderMarketplaceItemId,
                        principalTable: "OrderMarketplaceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketplaceAccruals_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccruals_CatalogItemId",
                table: "MarketplaceAccruals",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccruals_MarketplaceAccountId_Date",
                table: "MarketplaceAccruals",
                columns: new[] { "MarketplaceAccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccruals_MarketplaceAccountId_ExternalId_LineNo",
                table: "MarketplaceAccruals",
                columns: new[] { "MarketplaceAccountId", "ExternalId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccruals_MarketplaceAccountId_UnitNumber",
                table: "MarketplaceAccruals",
                columns: new[] { "MarketplaceAccountId", "UnitNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccruals_OrderId",
                table: "MarketplaceAccruals",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccruals_OrderMarketplaceItemId",
                table: "MarketplaceAccruals",
                column: "OrderMarketplaceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceAccruals");

            migrationBuilder.DropColumn(
                name: "AccrualsCreated",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "AccrualsProcessed",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "AccrualsUpdated",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "AccrualsFullPassAt",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "AccrualsSyncedAt",
                table: "MarketplaceAccounts");
        }
    }
}

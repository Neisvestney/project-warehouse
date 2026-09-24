using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnsCreated",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReturnsProcessed",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReturnsUpdated",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnsSyncedAt",
                table: "MarketplaceAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketplaceReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    PostingNumber = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderMarketplaceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    OfferId = table.Column<string>(type: "text", nullable: false),
                    Scheme = table.Column<int>(type: "integer", nullable: false),
                    RawScheme = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RawKind = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    RawStatus = table.Column<string>(type: "text", nullable: true),
                    StatusName = table.Column<string>(type: "text", nullable: true),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompensationStatus = table.Column<int>(type: "integer", nullable: true),
                    CompensationStatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceExternalId = table.Column<string>(type: "text", nullable: true),
                    ExemplarId = table.Column<string>(type: "text", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceReturns_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketplaceReturns_MarketplaceAccounts_MarketplaceAccountId",
                        column: x => x.MarketplaceAccountId,
                        principalTable: "MarketplaceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplaceReturns_OrderMarketplaceItems_OrderMarketplaceIt~",
                        column: x => x.OrderMarketplaceItemId,
                        principalTable: "OrderMarketplaceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketplaceReturns_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceReturns_CatalogItemId",
                table: "MarketplaceReturns",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceReturns_MarketplaceAccountId_ExternalId",
                table: "MarketplaceReturns",
                columns: new[] { "MarketplaceAccountId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceReturns_MarketplaceAccountId_PostingNumber",
                table: "MarketplaceReturns",
                columns: new[] { "MarketplaceAccountId", "PostingNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceReturns_OrderId",
                table: "MarketplaceReturns",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceReturns_OrderMarketplaceItemId",
                table: "MarketplaceReturns",
                column: "OrderMarketplaceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceReturns");

            migrationBuilder.DropColumn(
                name: "ReturnsCreated",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "ReturnsProcessed",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "ReturnsUpdated",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "ReturnsSyncedAt",
                table: "MarketplaceAccounts");
        }
    }
}

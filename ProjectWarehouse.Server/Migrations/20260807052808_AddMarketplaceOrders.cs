using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using ProjectWarehouse.Server.Models;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketplaceOrderId",
                table: "Orders");

            // Hand-edited: EF scaffolds an AlterColumn here, but Postgres has no implicit text -> uuid
            // cast and rejects it without a USING clause. Dropping is safe — nothing in the codebase ever
            // created an OrderMarketplaceItem, and the table was verified empty before this migration.
            migrationBuilder.DropColumn(
                name: "MarketplaceCardId",
                table: "OrderMarketplaceItems");

            migrationBuilder.AddColumn<Guid>(
                name: "MarketplaceCardId",
                table: "OrderMarketplaceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrdersCreated",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrdersProcessed",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrdersSkipped",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrdersUpdated",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<IList<SkippedOrderInfo>>(
                name: "SkippedOrders",
                table: "MarketplaceSyncRuns",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketplaceOrders",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingNumber = table.Column<string>(type: "text", nullable: false),
                    ExternalOrderNumber = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RawStatus = table.Column<string>(type: "text", nullable: true),
                    RawSubstatus = table.Column<string>(type: "text", nullable: true),
                    ShipmentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InProcessAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrackingNumber = table.Column<string>(type: "text", nullable: true),
                    DeliveryMethodName = table.Column<string>(type: "text", nullable: true),
                    MultiBoxQty = table.Column<int>(type: "integer", nullable: false),
                    LabelFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabelFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LabelError = table.Column<AppFieldError>(type: "jsonb", nullable: true),
                    StatusSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceOrders", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_MarketplaceOrders_DataFiles_LabelFileId",
                        column: x => x.LabelFileId,
                        principalTable: "DataFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceOrders_MarketplaceAccounts_MarketplaceAccountId",
                        column: x => x.MarketplaceAccountId,
                        principalTable: "MarketplaceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceOrders_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderMarketplaceItems_MarketplaceCardId",
                table: "OrderMarketplaceItems",
                column: "MarketplaceCardId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceCards_MarketplaceAccountId_Sku",
                table: "MarketplaceCards",
                columns: new[] { "MarketplaceAccountId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_LabelFileId",
                table: "MarketplaceOrders",
                column: "LabelFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_PostingNumber",
                table: "MarketplaceOrders",
                columns: new[] { "MarketplaceAccountId", "PostingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_Status",
                table: "MarketplaceOrders",
                columns: new[] { "MarketplaceAccountId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_OrderMarketplaceItems_MarketplaceCards_MarketplaceCardId",
                table: "OrderMarketplaceItems",
                column: "MarketplaceCardId",
                principalTable: "MarketplaceCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderMarketplaceItems_MarketplaceCards_MarketplaceCardId",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropTable(
                name: "MarketplaceOrders");

            migrationBuilder.DropIndex(
                name: "IX_OrderMarketplaceItems_MarketplaceCardId",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropIndex(
                name: "IX_MarketplaceCards_MarketplaceAccountId_Sku",
                table: "MarketplaceCards");

            migrationBuilder.DropColumn(
                name: "OrdersCreated",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "OrdersProcessed",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "OrdersSkipped",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "OrdersUpdated",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "SkippedOrders",
                table: "MarketplaceSyncRuns");

            migrationBuilder.AddColumn<string>(
                name: "MarketplaceOrderId",
                table: "Orders",
                type: "text",
                nullable: true);

            // uuid -> text has no implicit cast either, so the revert drops and re-adds as well
            migrationBuilder.DropColumn(
                name: "MarketplaceCardId",
                table: "OrderMarketplaceItems");

            migrationBuilder.AddColumn<string>(
                name: "MarketplaceCardId",
                table: "OrderMarketplaceItems",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}

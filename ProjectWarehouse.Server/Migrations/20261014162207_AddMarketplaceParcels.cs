using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ProjectWarehouse.Server.Models;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceParcels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScanitBarcode",
                table: "MarketplaceOrders",
                newName: "OzonScanitBarcode");

            migrationBuilder.AddColumn<int>(
                name: "OrdersConfirmed",
                table: "MarketplaceSyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MarketplaceParcels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    Barcode = table.Column<string>(type: "text", nullable: true),
                    OrderBoxId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabelFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabelFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LabelKind = table.Column<int>(type: "integer", nullable: true),
                    LabelError = table.Column<AppFieldError>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceParcels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceParcels_DataFiles_LabelFileId",
                        column: x => x.LabelFileId,
                        principalTable: "DataFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceParcels_MarketplaceOrders_MarketplaceOrderId",
                        column: x => x.MarketplaceOrderId,
                        principalTable: "MarketplaceOrders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplaceParcels_OrderBoxes_OrderBoxId",
                        column: x => x.OrderBoxId,
                        principalTable: "OrderBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceParcelItems",
                columns: table => new
                {
                    MarketplaceParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderMarketplaceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: true),
                    PartIndex = table.Column<int>(type: "integer", nullable: true),
                    PartCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceParcelItems", x => new { x.MarketplaceParcelId, x.OrderMarketplaceItemId });
                    table.ForeignKey(
                        name: "FK_MarketplaceParcelItems_MarketplaceParcels_MarketplaceParcel~",
                        column: x => x.MarketplaceParcelId,
                        principalTable: "MarketplaceParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplaceParcelItems_OrderMarketplaceItems_OrderMarketpla~",
                        column: x => x.OrderMarketplaceItemId,
                        principalTable: "OrderMarketplaceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceParcelItems_OrderMarketplaceItemId",
                table: "MarketplaceParcelItems",
                column: "OrderMarketplaceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceParcels_LabelFetchedAt",
                table: "MarketplaceParcels",
                column: "LabelFetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceParcels_LabelFileId",
                table: "MarketplaceParcels",
                column: "LabelFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceParcels_MarketplaceOrderId_Index",
                table: "MarketplaceParcels",
                columns: new[] { "MarketplaceOrderId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceParcels_OrderBoxId",
                table: "MarketplaceParcels",
                column: "OrderBoxId");

            // every posting is one parcel holding its existing label, coinciding with its box where it has one
            migrationBuilder.Sql("""
                INSERT INTO "MarketplaceParcels"
                    ("Id", "MarketplaceOrderId", "Index", "OrderBoxId",
                     "LabelFileId", "LabelFetchedAt", "LabelKind", "LabelError")
                SELECT gen_random_uuid(), mo."OrderId", 1,
                       (SELECT min(b."Id"::text)::uuid FROM "OrderBoxes" b
                        WHERE b."OrderId" = mo."OrderId"
                        HAVING count(*) = 1),
                       mo."LabelFileId", mo."LabelFetchedAt", mo."LabelKind", mo."LabelError"
                FROM "MarketplaceOrders" mo;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MarketplaceOrders_DataFiles_LabelFileId",
                table: "MarketplaceOrders");

            migrationBuilder.DropIndex(
                name: "IX_MarketplaceOrders_LabelFileId",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "LabelError",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "LabelFetchedAt",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "LabelFileId",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "LabelKind",
                table: "MarketplaceOrders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrdersConfirmed",
                table: "MarketplaceSyncRuns");

            migrationBuilder.RenameColumn(
                name: "OzonScanitBarcode",
                table: "MarketplaceOrders",
                newName: "ScanitBarcode");

            migrationBuilder.AddColumn<AppFieldError>(
                name: "LabelError",
                table: "MarketplaceOrders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LabelFetchedAt",
                table: "MarketplaceOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LabelFileId",
                table: "MarketplaceOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LabelKind",
                table: "MarketplaceOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "MarketplaceOrders" mo
                SET "LabelFileId" = p."LabelFileId", "LabelFetchedAt" = p."LabelFetchedAt",
                    "LabelKind" = p."LabelKind", "LabelError" = p."LabelError"
                FROM "MarketplaceParcels" p
                WHERE p."MarketplaceOrderId" = mo."OrderId" AND p."Index" = 1;
                """);

            migrationBuilder.DropTable(
                name: "MarketplaceParcelItems");

            migrationBuilder.DropTable(
                name: "MarketplaceParcels");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_LabelFileId",
                table: "MarketplaceOrders",
                column: "LabelFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_MarketplaceOrders_DataFiles_LabelFileId",
                table: "MarketplaceOrders",
                column: "LabelFileId",
                principalTable: "DataFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStockForecastSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsumptionWindowDays",
                table: "Warehouses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockWarningDays",
                table: "Warehouses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseWeightedConsumption",
                table: "Warehouses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CatalogItemStockWarnings",
                columns: table => new
                {
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarningDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemStockWarnings", x => new { x.CatalogItemId, x.WarehouseId });
                    table.ForeignKey(
                        name: "FK_CatalogItemStockWarnings_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItemStockWarnings_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemStockWarnings_WarehouseId",
                table: "CatalogItemStockWarnings",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItemStockWarnings");

            migrationBuilder.DropColumn(
                name: "ConsumptionWindowDays",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "StockWarningDays",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "UseWeightedConsumption",
                table: "Warehouses");
        }
    }
}

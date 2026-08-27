using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementUnitItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UnitInventoryItemId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitInventoryNumber",
                table: "StockMovements",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_UnitInventoryItemId",
                table: "StockMovements",
                column: "UnitInventoryItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_InventoryItems_UnitInventoryItemId",
                table: "StockMovements",
                column: "UnitInventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryItems_UnitInventoryItemId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_UnitInventoryItemId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "UnitInventoryItemId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "UnitInventoryNumber",
                table: "StockMovements");
        }
    }
}

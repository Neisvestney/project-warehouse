using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseDefaultStoragePlaceNode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultStoragePlaceNodeId",
                table: "Warehouses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultStoragePlaceNodeId",
                table: "Warehouses",
                column: "DefaultStoragePlaceNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_StoragePlacesNodes_DefaultStoragePlaceNodeId",
                table: "Warehouses",
                column: "DefaultStoragePlaceNodeId",
                principalTable: "StoragePlacesNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_StoragePlacesNodes_DefaultStoragePlaceNodeId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultStoragePlaceNodeId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultStoragePlaceNodeId",
                table: "Warehouses");
        }
    }
}

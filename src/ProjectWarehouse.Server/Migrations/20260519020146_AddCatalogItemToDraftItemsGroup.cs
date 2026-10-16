using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemToDraftItemsGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                table: "InboundOrderDraftItemsGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreateNew",
                table: "InboundOrderDraftItemsGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderDraftItemsGroups_CatalogItemId",
                table: "InboundOrderDraftItemsGroups",
                column: "CatalogItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderDraftItemsGroups_CatalogItems_CatalogItemId",
                table: "InboundOrderDraftItemsGroups",
                column: "CatalogItemId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderDraftItemsGroups_CatalogItems_CatalogItemId",
                table: "InboundOrderDraftItemsGroups");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrderDraftItemsGroups_CatalogItemId",
                table: "InboundOrderDraftItemsGroups");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "InboundOrderDraftItemsGroups");

            migrationBuilder.DropColumn(
                name: "CreateNew",
                table: "InboundOrderDraftItemsGroups");
        }
    }
}

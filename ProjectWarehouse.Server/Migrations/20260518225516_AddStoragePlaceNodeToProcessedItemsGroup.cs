using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStoragePlaceNodeToProcessedItemsGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderDraftItemsGroup_CatalogItemsWithCharacteristics~",
                table: "InboundOrderDraftItemsGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderDraftItemsGroup_InboundOrders_InboundOrderId",
                table: "InboundOrderDraftItemsGroup");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InboundOrderDraftItemsGroup",
                table: "InboundOrderDraftItemsGroup");

            migrationBuilder.RenameTable(
                name: "InboundOrderDraftItemsGroup",
                newName: "InboundOrderDraftItemsGroups");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrderDraftItemsGroup_InboundOrderId",
                table: "InboundOrderDraftItemsGroups",
                newName: "IX_InboundOrderDraftItemsGroups_InboundOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrderDraftItemsGroup_CatalogItemWithCharacteristicId",
                table: "InboundOrderDraftItemsGroups",
                newName: "IX_InboundOrderDraftItemsGroups_CatalogItemWithCharacteristicId");

            migrationBuilder.AddColumn<Guid>(
                name: "StoragePlaceNodeItemsGroup_StoragePlaceNodeId",
                table: "ItemsGroup",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_InboundOrderDraftItemsGroups",
                table: "InboundOrderDraftItemsGroups",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsGroup_StoragePlaceNodeItemsGroup_StoragePlaceNodeId",
                table: "ItemsGroup",
                column: "StoragePlaceNodeItemsGroup_StoragePlaceNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderDraftItemsGroups_CatalogItemsWithCharacteristic~",
                table: "InboundOrderDraftItemsGroups",
                column: "CatalogItemWithCharacteristicId",
                principalTable: "CatalogItemsWithCharacteristics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderDraftItemsGroups_InboundOrders_InboundOrderId",
                table: "InboundOrderDraftItemsGroups",
                column: "InboundOrderId",
                principalTable: "InboundOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_StoragePlacesNodes_StoragePlaceNodeItemsGroup_St~",
                table: "ItemsGroup",
                column: "StoragePlaceNodeItemsGroup_StoragePlaceNodeId",
                principalTable: "StoragePlacesNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderDraftItemsGroups_CatalogItemsWithCharacteristic~",
                table: "InboundOrderDraftItemsGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderDraftItemsGroups_InboundOrders_InboundOrderId",
                table: "InboundOrderDraftItemsGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_StoragePlacesNodes_StoragePlaceNodeItemsGroup_St~",
                table: "ItemsGroup");

            migrationBuilder.DropIndex(
                name: "IX_ItemsGroup_StoragePlaceNodeItemsGroup_StoragePlaceNodeId",
                table: "ItemsGroup");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InboundOrderDraftItemsGroups",
                table: "InboundOrderDraftItemsGroups");

            migrationBuilder.DropColumn(
                name: "StoragePlaceNodeItemsGroup_StoragePlaceNodeId",
                table: "ItemsGroup");

            migrationBuilder.RenameTable(
                name: "InboundOrderDraftItemsGroups",
                newName: "InboundOrderDraftItemsGroup");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrderDraftItemsGroups_InboundOrderId",
                table: "InboundOrderDraftItemsGroup",
                newName: "IX_InboundOrderDraftItemsGroup_InboundOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrderDraftItemsGroups_CatalogItemWithCharacteristicId",
                table: "InboundOrderDraftItemsGroup",
                newName: "IX_InboundOrderDraftItemsGroup_CatalogItemWithCharacteristicId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InboundOrderDraftItemsGroup",
                table: "InboundOrderDraftItemsGroup",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderDraftItemsGroup_CatalogItemsWithCharacteristics~",
                table: "InboundOrderDraftItemsGroup",
                column: "CatalogItemWithCharacteristicId",
                principalTable: "CatalogItemsWithCharacteristics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderDraftItemsGroup_InboundOrders_InboundOrderId",
                table: "InboundOrderDraftItemsGroup",
                column: "InboundOrderId",
                principalTable: "InboundOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

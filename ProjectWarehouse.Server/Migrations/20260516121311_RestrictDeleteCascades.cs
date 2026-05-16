using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class RestrictDeleteCascades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_CatalogItemsWithCharacteristics_CatalogItemWithC~",
                table: "ItemsGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_StoragePlacesNodes_StoragePlaceNodeId",
                table: "ItemsGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_StoragePlacesNodes_StoragePlaces_RootStoragePlaceId",
                table: "StoragePlacesNodes");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_CatalogItemsWithCharacteristics_CatalogItemWithC~",
                table: "ItemsGroup",
                column: "CatalogItemWithCharacteristicId",
                principalTable: "CatalogItemsWithCharacteristics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_StoragePlacesNodes_StoragePlaceNodeId",
                table: "ItemsGroup",
                column: "StoragePlaceNodeId",
                principalTable: "StoragePlacesNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoragePlacesNodes_StoragePlaces_RootStoragePlaceId",
                table: "StoragePlacesNodes",
                column: "RootStoragePlaceId",
                principalTable: "StoragePlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_CatalogItemsWithCharacteristics_CatalogItemWithC~",
                table: "ItemsGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_StoragePlacesNodes_StoragePlaceNodeId",
                table: "ItemsGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_StoragePlacesNodes_StoragePlaces_RootStoragePlaceId",
                table: "StoragePlacesNodes");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_CatalogItemsWithCharacteristics_CatalogItemWithC~",
                table: "ItemsGroup",
                column: "CatalogItemWithCharacteristicId",
                principalTable: "CatalogItemsWithCharacteristics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_StoragePlacesNodes_StoragePlaceNodeId",
                table: "ItemsGroup",
                column: "StoragePlaceNodeId",
                principalTable: "StoragePlacesNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StoragePlacesNodes_StoragePlaces_RootStoragePlaceId",
                table: "StoragePlacesNodes",
                column: "RootStoragePlaceId",
                principalTable: "StoragePlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

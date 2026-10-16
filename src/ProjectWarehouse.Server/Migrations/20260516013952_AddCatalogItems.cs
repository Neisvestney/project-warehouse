using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Article = table.Column<string>(type: "text", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItemsWithCharacteristics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Characteristic = table.Column<string>(type: "text", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: true),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemsWithCharacteristics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemsWithCharacteristics_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemsGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemWithCharacteristicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Discriminator = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    StoragePlaceNodeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemsGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemsGroup_CatalogItemsWithCharacteristics_CatalogItemWithC~",
                        column: x => x.CatalogItemWithCharacteristicId,
                        principalTable: "CatalogItemsWithCharacteristics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemsGroup_StoragePlacesNodes_StoragePlaceNodeId",
                        column: x => x.StoragePlaceNodeId,
                        principalTable: "StoragePlacesNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemsWithCharacteristics_CatalogItemId",
                table: "CatalogItemsWithCharacteristics",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsGroup_CatalogItemWithCharacteristicId",
                table: "ItemsGroup",
                column: "CatalogItemWithCharacteristicId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsGroup_StoragePlaceNodeId",
                table: "ItemsGroup",
                column: "StoragePlaceNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemsGroup");

            migrationBuilder.DropTable(
                name: "CatalogItemsWithCharacteristics");

            migrationBuilder.DropTable(
                name: "CatalogItems");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoragePlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoragePlaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoragePlaces_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoragePlacesNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RootStoragePlaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentNodeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoragePlacesNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoragePlacesNodes_StoragePlacesNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalTable: "StoragePlacesNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StoragePlacesNodes_StoragePlaces_RootStoragePlaceId",
                        column: x => x.RootStoragePlaceId,
                        principalTable: "StoragePlaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaces_WarehouseId",
                table: "StoragePlaces",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlacesNodes_ParentNodeId",
                table: "StoragePlacesNodes",
                column: "ParentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlacesNodes_RootStoragePlaceId",
                table: "StoragePlacesNodes",
                column: "RootStoragePlaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoragePlacesNodes");

            migrationBuilder.DropTable(
                name: "StoragePlaces");

            migrationBuilder.DropTable(
                name: "Warehouses");
        }
    }
}

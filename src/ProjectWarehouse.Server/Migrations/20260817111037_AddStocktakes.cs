using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stocktakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocktakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stocktakes_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Stocktakes_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StocktakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePlaceNodeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StocktakeNodes_Stocktakes_StocktakeId",
                        column: x => x.StocktakeId,
                        principalTable: "Stocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StocktakeNodes_StoragePlacesNodes_StoragePlaceNodeId",
                        column: x => x.StoragePlaceNodeId,
                        principalTable: "StoragePlacesNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StocktakeNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedQuantity = table.Column<int>(type: "integer", nullable: false),
                    InventoryNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AppliedDelta = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StocktakeItems_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StocktakeItems_InventoryItems_UnitInventoryItemId",
                        column: x => x.UnitInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StocktakeItems_StocktakeNodes_StocktakeNodeId",
                        column: x => x.StocktakeNodeId,
                        principalTable: "StocktakeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeItems_CatalogItemId",
                table: "StocktakeItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeItems_StocktakeNodeId",
                table: "StocktakeItems",
                column: "StocktakeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeItems_UnitInventoryItemId",
                table: "StocktakeItems",
                column: "UnitInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeNodes_StocktakeId_StoragePlaceNodeId",
                table: "StocktakeNodes",
                columns: new[] { "StocktakeId", "StoragePlaceNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeNodes_StoragePlaceNodeId",
                table: "StocktakeNodes",
                column: "StoragePlaceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_CreatedById",
                table: "Stocktakes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_Number",
                table: "Stocktakes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_WarehouseId",
                table: "Stocktakes",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StocktakeItems");

            migrationBuilder.DropTable(
                name: "StocktakeNodes");

            migrationBuilder.DropTable(
                name: "Stocktakes");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWriteoffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Writeoffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Writeoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Writeoffs_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Writeoffs_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WriteoffItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WriteoffId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssembledBundleInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WriteoffItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WriteoffItems_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WriteoffItems_InventoryItems_AssembledBundleInventoryItemId",
                        column: x => x.AssembledBundleInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WriteoffItems_InventoryItems_UnitInventoryItemId",
                        column: x => x.UnitInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WriteoffItems_StoragePlacesNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalTable: "StoragePlacesNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WriteoffItems_Writeoffs_WriteoffId",
                        column: x => x.WriteoffId,
                        principalTable: "Writeoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_AssembledBundleInventoryItemId",
                table: "WriteoffItems",
                column: "AssembledBundleInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_CatalogItemId",
                table: "WriteoffItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_SourceNodeId",
                table: "WriteoffItems",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_UnitInventoryItemId",
                table: "WriteoffItems",
                column: "UnitInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_WriteoffId",
                table: "WriteoffItems",
                column: "WriteoffId");

            migrationBuilder.CreateIndex(
                name: "IX_Writeoffs_CreatedById",
                table: "Writeoffs",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Writeoffs_Number",
                table: "Writeoffs",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Writeoffs_WarehouseId",
                table: "Writeoffs",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WriteoffItems");

            migrationBuilder.DropTable(
                name: "Writeoffs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MainImageFileId",
                table: "CatalogItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatalogItemImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemImages_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItemImages_DataFiles_DataFileId",
                        column: x => x.DataFileId,
                        principalTable: "DataFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_MainImageFileId",
                table: "CatalogItems",
                column: "MainImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemImages_CatalogItemId_Order",
                table: "CatalogItemImages",
                columns: new[] { "CatalogItemId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemImages_DataFileId",
                table: "CatalogItemImages",
                column: "DataFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItems_DataFiles_MainImageFileId",
                table: "CatalogItems",
                column: "MainImageFileId",
                principalTable: "DataFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItems_DataFiles_MainImageFileId",
                table: "CatalogItems");

            migrationBuilder.DropTable(
                name: "CatalogItemImages");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_MainImageFileId",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "MainImageFileId",
                table: "CatalogItems");
        }
    }
}

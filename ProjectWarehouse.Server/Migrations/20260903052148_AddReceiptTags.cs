using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItemTagLinks_CatalogItemTags_TagsId",
                table: "CatalogItemTagLinks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CatalogItemTags",
                table: "CatalogItemTags");

            migrationBuilder.RenameTable(
                name: "CatalogItemTags",
                newName: "Tags");

            migrationBuilder.AddColumn<string>(
                name: "TagType",
                table: "Tags",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "CatalogItem");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tags",
                table: "Tags",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ReceiptTagLinks",
                columns: table => new
                {
                    ReceiptsId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptTagLinks", x => new { x.ReceiptsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ReceiptTagLinks_Receipts_ReceiptsId",
                        column: x => x.ReceiptsId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptTagLinks_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTagLinks_TagsId",
                table: "ReceiptTagLinks",
                column: "TagsId");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItemTagLinks_Tags_TagsId",
                table: "CatalogItemTagLinks",
                column: "TagsId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItemTagLinks_Tags_TagsId",
                table: "CatalogItemTagLinks");

            migrationBuilder.DropTable(
                name: "ReceiptTagLinks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tags",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "TagType",
                table: "Tags");

            migrationBuilder.RenameTable(
                name: "Tags",
                newName: "CatalogItemTags");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CatalogItemTags",
                table: "CatalogItemTags",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItemTagLinks_CatalogItemTags_TagsId",
                table: "CatalogItemTagLinks",
                column: "TagsId",
                principalTable: "CatalogItemTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

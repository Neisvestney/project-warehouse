using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StocktakeId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WriteoffId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderTagLinks",
                columns: table => new
                {
                    OrdersId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTagLinks", x => new { x.OrdersId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_OrderTagLinks_Orders_OrdersId",
                        column: x => x.OrdersId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderTagLinks_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeTagLinks",
                columns: table => new
                {
                    StocktakesId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeTagLinks", x => new { x.StocktakesId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_StocktakeTagLinks_Stocktakes_StocktakesId",
                        column: x => x.StocktakesId,
                        principalTable: "Stocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StocktakeTagLinks_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WriteoffTagLinks",
                columns: table => new
                {
                    TagsId = table.Column<Guid>(type: "uuid", nullable: false),
                    WriteoffsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WriteoffTagLinks", x => new { x.TagsId, x.WriteoffsId });
                    table.ForeignKey(
                        name: "FK_WriteoffTagLinks_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WriteoffTagLinks_Writeoffs_WriteoffsId",
                        column: x => x.WriteoffsId,
                        principalTable: "Writeoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrderId",
                table: "StockMovements",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StocktakeId",
                table: "StockMovements",
                column: "StocktakeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WriteoffId",
                table: "StockMovements",
                column: "WriteoffId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTagLinks_TagsId",
                table: "OrderTagLinks",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeTagLinks_TagsId",
                table: "StocktakeTagLinks",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffTagLinks_WriteoffsId",
                table: "WriteoffTagLinks",
                column: "WriteoffsId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Orders_OrderId",
                table: "StockMovements",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Stocktakes_StocktakeId",
                table: "StockMovements",
                column: "StocktakeId",
                principalTable: "Stocktakes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Writeoffs_WriteoffId",
                table: "StockMovements",
                column: "WriteoffId",
                principalTable: "Writeoffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Orders_OrderId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Stocktakes_StocktakeId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Writeoffs_WriteoffId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "OrderTagLinks");

            migrationBuilder.DropTable(
                name: "StocktakeTagLinks");

            migrationBuilder.DropTable(
                name: "WriteoffTagLinks");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OrderId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_StocktakeId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_WriteoffId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "StocktakeId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "WriteoffId",
                table: "StockMovements");
        }
    }
}

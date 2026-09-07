using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDataFileAttachmentsToOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderImages_DataFiles_DataFileId",
                        column: x => x.DataFileId,
                        principalTable: "DataFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderImages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptImages_DataFiles_DataFileId",
                        column: x => x.DataFileId,
                        principalTable: "DataFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptImages_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StocktakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StocktakeImages_DataFiles_DataFileId",
                        column: x => x.DataFileId,
                        principalTable: "DataFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StocktakeImages_Stocktakes_StocktakeId",
                        column: x => x.StocktakeId,
                        principalTable: "Stocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WriteoffImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WriteoffId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WriteoffImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WriteoffImages_DataFiles_DataFileId",
                        column: x => x.DataFileId,
                        principalTable: "DataFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WriteoffImages_Writeoffs_WriteoffId",
                        column: x => x.WriteoffId,
                        principalTable: "Writeoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderImages_DataFileId",
                table: "OrderImages",
                column: "DataFileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderImages_OrderId_Order",
                table: "OrderImages",
                columns: new[] { "OrderId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptImages_DataFileId",
                table: "ReceiptImages",
                column: "DataFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptImages_ReceiptId_Order",
                table: "ReceiptImages",
                columns: new[] { "ReceiptId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeImages_DataFileId",
                table: "StocktakeImages",
                column: "DataFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeImages_StocktakeId_Order",
                table: "StocktakeImages",
                columns: new[] { "StocktakeId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffImages_DataFileId",
                table: "WriteoffImages",
                column: "DataFileId");

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffImages_WriteoffId_Order",
                table: "WriteoffImages",
                columns: new[] { "WriteoffId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderImages");

            migrationBuilder.DropTable(
                name: "ReceiptImages");

            migrationBuilder.DropTable(
                name: "StocktakeImages");

            migrationBuilder.DropTable(
                name: "WriteoffImages");
        }
    }
}

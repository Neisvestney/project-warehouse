using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptItemOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_ReceiptId",
                table: "ReceiptItems");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "ReceiptItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ReceiptId_Order",
                table: "ReceiptItems",
                columns: new[] { "ReceiptId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_ReceiptId_Order",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "ReceiptItems");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ReceiptId",
                table: "ReceiptItems",
                column: "ReceiptId");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWriteoffItemOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WriteoffItems_WriteoffId",
                table: "WriteoffItems");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "WriteoffItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_WriteoffId_Order",
                table: "WriteoffItems",
                columns: new[] { "WriteoffId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WriteoffItems_WriteoffId_Order",
                table: "WriteoffItems");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "WriteoffItems");

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_WriteoffId",
                table: "WriteoffItems",
                column: "WriteoffId");
        }
    }
}

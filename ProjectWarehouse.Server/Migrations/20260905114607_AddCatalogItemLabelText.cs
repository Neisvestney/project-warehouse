using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemLabelText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabelText",
                table: "CatalogItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelText",
                table: "CatalogItems");
        }
    }
}

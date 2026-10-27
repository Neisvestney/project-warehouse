using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class RenameOzonScanitBarcodeToLabelBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OzonScanitBarcode",
                table: "MarketplaceOrders",
                newName: "LabelBarcode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LabelBarcode",
                table: "MarketplaceOrders",
                newName: "OzonScanitBarcode");
        }
    }
}

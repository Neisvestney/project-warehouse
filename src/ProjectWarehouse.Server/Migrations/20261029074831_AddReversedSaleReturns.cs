using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddReversedSaleReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SaleReversedOn",
                table: "MarketplaceReturns",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExemplarId",
                table: "MarketplaceAccruals",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_LabelBarcode",
                table: "MarketplaceOrders",
                columns: new[] { "MarketplaceAccountId", "LabelBarcode" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccruals_MarketplaceAccountId_ExemplarId",
                table: "MarketplaceAccruals",
                columns: new[] { "MarketplaceAccountId", "ExemplarId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_LabelBarcode",
                table: "MarketplaceOrders");

            migrationBuilder.DropIndex(
                name: "IX_MarketplaceAccruals_MarketplaceAccountId_ExemplarId",
                table: "MarketplaceAccruals");

            migrationBuilder.DropColumn(
                name: "SaleReversedOn",
                table: "MarketplaceReturns");

            migrationBuilder.DropColumn(
                name: "ExemplarId",
                table: "MarketplaceAccruals");
        }
    }
}

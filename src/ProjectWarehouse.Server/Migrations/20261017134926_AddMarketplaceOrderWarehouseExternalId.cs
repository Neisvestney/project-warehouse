using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceOrderWarehouseExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WarehouseExternalId",
                table: "MarketplaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_WarehouseExternalId",
                table: "MarketplaceOrders",
                columns: new[] { "MarketplaceAccountId", "WarehouseExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_WarehouseExternalId",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "WarehouseExternalId",
                table: "MarketplaceOrders");
        }
    }
}

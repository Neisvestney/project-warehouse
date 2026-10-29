using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceOrderAndAccrualRid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rid",
                table: "MarketplaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rid",
                table: "MarketplaceAccruals",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_Rid",
                table: "MarketplaceOrders",
                columns: new[] { "MarketplaceAccountId", "Rid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketplaceOrders_MarketplaceAccountId_Rid",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "Rid",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "Rid",
                table: "MarketplaceAccruals");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceOrderCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "MarketplaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancellationType",
                table: "MarketplaceOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CancelledAfterShip",
                table: "MarketplaceOrders",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawCancellationType",
                table: "MarketplaceOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "CancellationType",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "CancelledAfterShip",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "RawCancellationType",
                table: "MarketplaceOrders");
        }
    }
}
